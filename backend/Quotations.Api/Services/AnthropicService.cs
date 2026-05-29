using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Quotations.Api.Services;

public record AiAnalysisRequestPreview(string Model, int MaxTokens, string Prompt, string RequestJson);

public record BatchSubmitResult(string AnthropicBatchId, int RequestCount);
public record BatchStatusResult(string AnthropicBatchId, string ProcessingStatus, int Succeeded, int Errored, int Expired, string? ResultsUrl);
public record BatchMessageResult(string CustomId, bool Succeeded, string? ContentText, string? ErrorType);

public interface IAnthropicService
{
    Task<AiAnalysisResult?> AnalyzeQuotationAsync(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear,
        bool useWebSearch,
        string? modelOverride = null);

    AiAnalysisRequestPreview BuildRequestPreview(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear,
        bool useWebSearch);

    // Batch API — 50% cost discount, results arrive asynchronously (minutes to hours)
    Task<BatchSubmitResult> SubmitBatchAsync(IEnumerable<(string QuotationId, string Text, string AuthorName, string SourceTitle, string SourceType)> requests);
    Task<BatchStatusResult> GetBatchStatusAsync(string anthropicBatchId);
    Task<List<BatchMessageResult>> GetBatchResultsAsync(string anthropicBatchId);

    // Parse the text content from a single batch result message
    AiAnalysisResult? ParseBatchResultContent(string contentText, string modelUsed);

    // Two-pass review: triage scores + tags, then targeted fixes for low-scoring fields
    Task<AiTriageResult?> TriageQuotationAsync(string text, string authorName, string sourceTitle, string sourceType);
    Task<AiFixResult?> FixFieldAsync(string field, string text, string authorName, string sourceTitle, string sourceType, bool useWebSearch);

    // Parse batch results for two-phase batch processing
    AiTriageResult? ParseTriageBatchResult(string contentText, string modelUsed);
    AiFixResult? ParseFixBatchResult(string contentText, string modelUsed);

    // Two-phase batch API
    Task<BatchSubmitResult> SubmitTriageBatchAsync(IEnumerable<(string QuotationId, string Text, string AuthorName, string SourceTitle, string SourceType)> requests);
    Task<BatchSubmitResult> SubmitFixBatchAsync(IEnumerable<(string QuotationId, string Field, string Text, string AuthorName, string SourceTitle, string SourceType)> requests);
}

public record AiScoreResult(
    int Score,
    string Reasoning,
    string? SuggestedValue,
    int? SuggestionConfidence,
    bool WasAiFilled,
    List<string> Citations);

public record AiTagSuggestion(string Tag, int Confidence);

public record AiAnalysisResult(
    AiScoreResult QuoteAccuracy,
    AiScoreResult AttributionAccuracy,
    AiScoreResult SourceAccuracy,
    string Summary,
    List<AiTagSuggestion> TagSuggestions,
    string ModelUsed,
    bool? IsLikelyAuthentic,
    string? AuthenticityReasoning,
    string? ApproximateEra,
    List<string> KnownVariants);

public record AiTriageResult(
    int QuoteScore,
    int AttributionScore,
    int SourceScore,
    List<AiTagSuggestion> Tags,
    string ModelUsed);

public record AiFixResult(
    string? SuggestedValue,
    int? Confidence,
    bool WasAiFilled,
    string Reasoning,
    string ModelUsed,
    List<string>? Citations = null);

public class AnthropicService : IAnthropicService
{
    // Shared across ALL Anthropic callers (AI Review + Chat) — 20 req/min keeps us safely
    // under free-tier limits and ensures Chat can always get through alongside AI Review.
    internal static readonly TokenBucketRateLimiter RateLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 20,
        TokensPerPeriod = 20,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 200,
        AutoReplenishment = true,
    });

    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicService> _logger;
    private readonly string _apiKey;
    private readonly AiReviewOptions _options;
    private const string ApiVersion = "2023-06-01";

    public AnthropicService(HttpClient httpClient, IConfiguration configuration, IOptions<AiReviewOptions> options, ILogger<AnthropicService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _apiKey = (configuration["Anthropic:ApiKey"] ?? string.Empty).Trim();
    }

    public AiAnalysisRequestPreview BuildRequestPreview(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear,
        bool useWebSearch)
    {
        var prompt = BuildPrompt(text, authorName, authorLifespan, sourceTitle, sourceType, sourceYear);
        var tools = useWebSearch
            ? new[] { new { type = "web_search_20250305", name = "web_search", max_uses = 5 } }
            : Array.Empty<object>();
        var requestBody = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            messages = new[] { new { role = "user", content = prompt } },
            tools
        };
        var requestJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true });
        return new AiAnalysisRequestPreview(_options.Model, _options.MaxTokens, prompt, requestJson);
    }

    public async Task<AiAnalysisResult?> AnalyzeQuotationAsync(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear,
        bool useWebSearch,
        string? modelOverride = null)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
        {
            _logger.LogWarning("Anthropic API key not configured — skipping AI analysis");
            return null;
        }

        using var lease = await RateLimiter.AcquireAsync(permitCount: 1);
        if (!lease.IsAcquired)
            throw new InvalidOperationException("Anthropic rate-limit queue is full — too many requests queued");

        var quoteContext = BuildQuoteContext(text, authorName, authorLifespan, sourceTitle, sourceType, sourceYear);
        var effectiveModel = modelOverride ?? _options.Model;

        // Split content into a cached static block + dynamic per-quote block.
        // The static instructions (~1,000 tokens) are reused across all requests within
        // a 5-min cache window, cutting input token cost by ~90% for the static portion.
        var cachedUserContent = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = StaticInstructions,
                ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
            },
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = quoteContext
            }
        };

        // Maintain full message history to support pause_turn continuation
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["content"] = cachedUserContent }
        };

        var toolsJson = useWebSearch
            ? """[{"type":"web_search_20250305","name":"web_search","max_uses":2}]"""
            : "[]";
        string contentText = string.Empty;
        string modelUsed = effectiveModel;

        for (int iteration = 0; iteration < 15; iteration++)
        {
            var requestNode = new JsonObject
            {
                ["model"] = effectiveModel,
                ["max_tokens"] = _options.MaxTokens,
                ["messages"] = JsonNode.Parse(messages.ToJsonString()),
                ["tools"] = JsonNode.Parse(toolsJson)
            };

            var responseBody = await SendWithRetryAsync(requestNode.ToJsonString());

            using var doc = JsonDocument.Parse(responseBody);
            var stopReason = doc.RootElement.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : null;
            modelUsed = doc.RootElement.TryGetProperty("model", out var m) ? m.GetString() ?? effectiveModel : effectiveModel;

            var contentEl = doc.RootElement.GetProperty("content");

            // Accumulate text from all text-type content blocks
            foreach (var block in contentEl.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "text"
                    && block.TryGetProperty("text", out var textEl))
                {
                    contentText += textEl.GetString();
                }
            }

            if (stopReason != "pause_turn")
                break;

            // Append the assistant's full content array and loop to continue
            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = JsonNode.Parse(contentEl.GetRawText())
            });

            _logger.LogInformation("Anthropic pause_turn on iteration {Iteration}, continuing", iteration + 1);
        }

        return ParseAnalysisResult(contentText, modelUsed);
    }

    public async Task<AiTriageResult?> TriageQuotationAsync(
        string text, string authorName, string sourceTitle, string sourceType)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
        {
            _logger.LogWarning("Anthropic API key not configured — skipping triage");
            return null;
        }

        using var lease = await RateLimiter.AcquireAsync(permitCount: 1);
        if (!lease.IsAcquired)
            throw new InvalidOperationException("Anthropic rate-limit queue is full");

        var quoteContext = BuildQuoteContext(text, authorName, null, sourceTitle, sourceType, null);
        var requestNode = new JsonObject
        {
            ["model"] = _options.Model,
            ["max_tokens"] = 256,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = TriageInstructions + "\n\n" + quoteContext
                }
            },
            ["tools"] = JsonNode.Parse("[]")
        };

        var responseBody = await SendWithRetryAsync(requestNode.ToJsonString());
        using var doc = JsonDocument.Parse(responseBody);
        var modelUsed = doc.RootElement.TryGetProperty("model", out var m) ? m.GetString() ?? _options.Model : _options.Model;
        var contentText = string.Empty;
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                && block.TryGetProperty("text", out var tx))
                contentText += tx.GetString();
        }

        return ParseTriageResult(contentText, modelUsed);
    }

    public async Task<AiFixResult?> FixFieldAsync(
        string field, string text, string authorName, string sourceTitle, string sourceType, bool useWebSearch)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
        {
            _logger.LogWarning("Anthropic API key not configured — skipping fix");
            return null;
        }

        using var lease = await RateLimiter.AcquireAsync(permitCount: 1);
        if (!lease.IsAcquired)
            throw new InvalidOperationException("Anthropic rate-limit queue is full");

        var instructions = field switch
        {
            "quote"  => QuoteFixInstructions,
            "author" => AuthorFixInstructions,
            "source" => SourceFixInstructions,
            _        => throw new ArgumentException($"Unknown field: {field}")
        };

        var quoteContext = BuildQuoteContext(text, authorName, null, sourceTitle, sourceType, null);
        var toolsJson = useWebSearch
            ? """[{"type":"web_search_20250305","name":"web_search","max_uses":2}]"""
            : "[]";

        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = instructions + "\n\n" + quoteContext
            }
        };

        string contentText = string.Empty;
        string modelUsed = _options.Model;

        for (int iteration = 0; iteration < 15; iteration++)
        {
            var requestNode = new JsonObject
            {
                ["model"] = _options.Model,
                ["max_tokens"] = 256,
                ["messages"] = JsonNode.Parse(messages.ToJsonString()),
                ["tools"] = JsonNode.Parse(toolsJson)
            };

            var responseBody = await SendWithRetryAsync(requestNode.ToJsonString());
            using var doc = JsonDocument.Parse(responseBody);
            var stopReason = doc.RootElement.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : null;
            modelUsed = doc.RootElement.TryGetProperty("model", out var m) ? m.GetString() ?? modelUsed : modelUsed;

            var contentEl = doc.RootElement.GetProperty("content");
            foreach (var block in contentEl.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && block.TryGetProperty("text", out var tx))
                    contentText += tx.GetString();
            }

            if (stopReason != "pause_turn") break;

            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = JsonNode.Parse(contentEl.GetRawText())
            });
        }

        return ParseFixResult(contentText, modelUsed);
    }

    private static AiTriageResult? ParseTriageResult(string contentText, string modelUsed)
    {
        try
        {
            var start = contentText.IndexOf('{');
            var end = contentText.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            using var doc = JsonDocument.Parse(contentText[start..(end + 1)]);
            var root = doc.RootElement;

            var quoteScore = root.TryGetProperty("quoteAccuracy", out var q) ? q.GetInt32() : 0;
            var attributionScore = root.TryGetProperty("attributionAccuracy", out var a) ? a.GetInt32() : 0;
            var sourceScore = root.TryGetProperty("sourceAccuracy", out var s) ? s.GetInt32() : 0;

            var tags = new List<AiTagSuggestion>();
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tagsEl.EnumerateArray())
                {
                    var tag = item.TryGetProperty("tag", out var t) ? t.GetString() : null;
                    var confidence = item.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                        ? c.GetInt32() : 0;
                    if (!string.IsNullOrWhiteSpace(tag) && CanonicalTags.All.Contains(tag))
                        tags.Add(new AiTagSuggestion(tag, confidence));
                }
            }

            return new AiTriageResult(quoteScore, attributionScore, sourceScore, tags, modelUsed);
        }
        catch { return null; }
    }

    private static AiFixResult? ParseFixResult(string contentText, string modelUsed)
    {
        try
        {
            var start = contentText.IndexOf('{');
            var end = contentText.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            using var doc = JsonDocument.Parse(contentText[start..(end + 1)]);
            var root = doc.RootElement;

            var suggested = root.TryGetProperty("suggestedValue", out var sv) && sv.ValueKind != JsonValueKind.Null
                ? sv.GetString() : null;
            int? confidence = root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetInt32() : null;
            var wasFilled = root.TryGetProperty("wasAiFilled", out var wf) && wf.GetBoolean();
            var reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() ?? string.Empty : string.Empty;

            return new AiFixResult(suggested, confidence, wasFilled, reasoning, modelUsed);
        }
        catch { return null; }
    }

    private async Task<string> SendWithRetryAsync(string json)
    {
        const int MaxRetries = 4;
        var delay = TimeSpan.FromSeconds(65); // safe default: just past the 1-min TPM window

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", ApiVersion);
            request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            await response.Content.LoadIntoBufferAsync();
            var body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt >= MaxRetries)
                    throw new InvalidOperationException($"Anthropic rate limit exceeded after {MaxRetries} retries: {body}");

                // Respect Retry-After if the API sends it, otherwise use our default
                if (response.Headers.RetryAfter?.Delta is { } retryAfterDelta)
                    delay = retryAfterDelta + TimeSpan.FromSeconds(2); // small buffer

                _logger.LogWarning(
                    "Anthropic rate limit hit (attempt {Attempt}/{Max}), waiting {Seconds}s before retry",
                    attempt + 1, MaxRetries, (int)delay.TotalSeconds);

                await Task.Delay(delay);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Anthropic API returned {StatusCode} (Content-Type: {ContentType}): {Body}",
                    (int)response.StatusCode,
                    response.Content.Headers.ContentType?.ToString() ?? "unknown",
                    string.IsNullOrWhiteSpace(body) ? "<empty body>" : body);
                throw new InvalidOperationException($"Anthropic API error {response.StatusCode}: {body}");
            }

            return body;
        }

        throw new InvalidOperationException("Unreachable");
    }

    // Cached once per process — identical for every request, so there's no reason to rebuild it.
    private static readonly string StaticInstructions = BuildStaticInstructions();
    private static readonly string TriageInstructions = BuildTriageInstructions();
    private static readonly string QuoteFixInstructions = BuildFixInstructions("quote");
    private static readonly string AuthorFixInstructions = BuildFixInstructions("author");
    private static readonly string SourceFixInstructions = BuildFixInstructions("source");

    private static string BuildStaticInstructions()
    {
        var tagList = string.Join(", ", CanonicalTags.All);

        return
            "You are a quotation accuracy expert. Analyze the quotation provided and respond ONLY with valid JSON.\n\n" +
            "Evaluate three dimensions and score each 1–10:\n" +
            "  quoteAccuracy       — Is this the exact wording typically associated with this person?\n" +
            "  attributionAccuracy — Did this specific person actually say or write this?\n" +
            "  sourceAccuracy      — Is this source title and type correct for this quote?\n\n" +
            "MISSING DATA RULES (highest priority — apply before scoring):\n" +
            "  - If the author field is 'Unknown', 'Anonymous', blank, or a clear placeholder: search the quotation text to identify the real author.\n" +
            "    Score attributionAccuracy at 1. Always provide suggestedValue with the correct name and set wasAiFilled=true.\n" +
            "  - If the source field is 'Other', 'Unknown', blank, or a clear placeholder: research the quotation to find the actual source title.\n" +
            "    Score sourceAccuracy at 1. Always provide suggestedValue with the correct title and set wasAiFilled=true.\n" +
            "  - If both author and source are unknown, prioritize finding the author first — a known author usually leads you to the source.\n\n" +
            "SUGGESTION RULES (apply to all three dimensions):\n" +
            "  - Score >= 8: set suggestedValue to null and omit suggestionConfidence (no fix needed).\n" +
            "  - Score 5–7: provide suggestedValue if you know a better/corrected version; set suggestionConfidence (0–100) to your confidence it is more accurate than the original.\n" +
            "  - Score <= 4: always provide suggestedValue with the correct version; set suggestionConfidence (0–100) accordingly.\n" +
            "  - Set wasAiFilled to true only when the original field was blank/unknown and you are supplying a known value.\n\n" +
            "CITATION RULES (apply to each dimension independently):\n" +
            "  - Provide citations only for claims you can verify.\n" +
            "  - High confidence (score 8–10): cite as specifically as possible — book title, edition, page if known.\n" +
            "  - Medium confidence (score 5–7): cite the reference but omit page details.\n" +
            "  - Low confidence (score 1–4): omit citations entirely (empty array) — do not guess.\n\n" +
            "TAG RULES:\n" +
            $"  Select 1–5 tags from this exact list: {tagList}\n" +
            "  Return only tags from that list, no others.\n" +
            "  For each tag include a confidence (0–100): your confidence it genuinely applies.\n\n" +
            "AUTHENTICITY METADATA:\n" +
            "  - isLikelyAuthentic: true if genuinely from the attributed author; false if likely misattributed or apocryphal.\n" +
            "  - authenticityReasoning: one or two sentences explaining your assessment.\n" +
            "  - approximateEra: short phrase placing the quotation historically (e.g. \"Ancient Greece, ~400 BCE\", \"Victorian era, 1880s\").\n" +
            "  - knownVariants: alternate wordings commonly found in the wild (empty array if none).\n\n" +
            "Respond with JSON only (no extra text). Shape:\n" +
            "{ quoteAccuracy: { score, reasoning, suggestedValue, suggestionConfidence, wasAiFilled, citations[] },\n" +
            "  attributionAccuracy: { score, reasoning, suggestedValue, suggestionConfidence, wasAiFilled, citations[] },\n" +
            "  sourceAccuracy: { score, reasoning, suggestedValue, suggestionConfidence, wasAiFilled, citations[] },\n" +
            "  suggestedTags: [{ tag, confidence }], summary, isLikelyAuthentic, authenticityReasoning, approximateEra, knownVariants[] }";
    }

    private static string BuildTriageInstructions()
    {
        var tagList = string.Join(", ", CanonicalTags.All);
        return
            "You are a quotation accuracy expert. Score each dimension 1-10 and assign tags.\n\n" +
            "quoteAccuracy — Is this the exact wording typically associated with this person?\n" +
            "attributionAccuracy — Did this specific person actually say or write this?\n" +
            "sourceAccuracy — Is this source title and type correct for this quote?\n\n" +
            "TAG RULES:\n" +
            $"  Select 1-5 tags from this exact list: {tagList}\n" +
            "  Return only tags from that list.\n" +
            "  For each tag include confidence (0-100): your confidence it genuinely applies.\n\n" +
            "Respond with JSON only:\n" +
            "{ \"quoteAccuracy\": 0, \"attributionAccuracy\": 0, \"sourceAccuracy\": 0, \"tags\": [{\"tag\": \"\", \"confidence\": 0}] }";
    }

    private static string BuildFixInstructions(string field)
    {
        var (subject, placeholder) = field switch
        {
            "quote"  => ("the exact wording of the quotation", "the corrected quote text"),
            "author" => ("the author attribution", "the correct author name"),
            "source" => ("the source title", "the correct source title"),
            _        => throw new ArgumentException($"Unknown field: {field}")
        };

        return
            $"You are a quotation accuracy expert. The {subject} for this quotation is wrong or missing.\n" +
            $"Identify {placeholder}.\n\n" +
            "Set wasAiFilled to true only if the original field was blank, 'Unknown', 'Anonymous', or a clear placeholder.\n\n" +
            "Respond with JSON only:\n" +
            "{ \"suggestedValue\": \"\", \"confidence\": 0, \"wasAiFilled\": false, \"reasoning\": \"\" }";
    }

    private static string BuildQuoteContext(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear)
    {
        var lifespanPart = !string.IsNullOrEmpty(authorLifespan) ? $" ({authorLifespan})" : string.Empty;
        var yearPart = sourceYear.HasValue ? $", {sourceYear}" : string.Empty;

        return
            $"Quotation text: \"{text}\"\n" +
            $"Attributed to: {authorName}{lifespanPart}\n" +
            $"Source: {sourceTitle} ({sourceType}{yearPart})";
    }

    // Kept for backward compatibility with BuildRequestPreview
    private static string BuildPrompt(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear)
        => StaticInstructions + "\n\n" + BuildQuoteContext(text, authorName, authorLifespan, sourceTitle, sourceType, sourceYear);

    public AiAnalysisResult? ParseBatchResultContent(string contentText, string modelUsed)
        => ParseAnalysisResult(contentText, modelUsed);

    private AiAnalysisResult? ParseAnalysisResult(string contentText, string modelUsed)
    {
        try
        {
            var start = contentText.IndexOf('{');
            var end = contentText.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            var jsonSlice = contentText[start..(end + 1)];
            using var doc = JsonDocument.Parse(jsonSlice);
            var root = doc.RootElement;

            var tagSuggestions = ParseTagSuggestions(root);

            var summary = root.TryGetProperty("summary", out var summaryEl)
                ? summaryEl.GetString() ?? string.Empty
                : string.Empty;

            bool? isLikelyAuthentic = null;
            if (root.TryGetProperty("isLikelyAuthentic", out var ilaEl) &&
                (ilaEl.ValueKind == JsonValueKind.True || ilaEl.ValueKind == JsonValueKind.False))
            {
                isLikelyAuthentic = ilaEl.GetBoolean();
            }

            var authenticityReasoning = root.TryGetProperty("authenticityReasoning", out var arEl)
                ? arEl.GetString()
                : null;

            var approximateEra = root.TryGetProperty("approximateEra", out var aeEl)
                ? aeEl.GetString()
                : null;

            var knownVariants = ParseStringArray(root, "knownVariants");

            return new AiAnalysisResult(
                ParseScore(root, "quoteAccuracy"),
                ParseScore(root, "attributionAccuracy"),
                ParseScore(root, "sourceAccuracy"),
                summary,
                tagSuggestions,
                modelUsed,
                isLikelyAuthentic,
                authenticityReasoning,
                approximateEra,
                knownVariants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Anthropic response: {Content}", contentText);
            return null;
        }
    }

    private static AiScoreResult ParseScore(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el))
            return new AiScoreResult(0, "Not evaluated", null, null, false, new List<string>());

        var score = el.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
        var reasoning = el.TryGetProperty("reasoning", out var r) ? r.GetString() ?? string.Empty : string.Empty;
        var suggested = el.TryGetProperty("suggestedValue", out var sv) && sv.ValueKind != JsonValueKind.Null
            ? sv.GetString()
            : null;
        int? suggestionConfidence = el.TryGetProperty("suggestionConfidence", out var sc) && sc.ValueKind == JsonValueKind.Number
            ? sc.GetInt32()
            : null;
        var wasFilled = el.TryGetProperty("wasAiFilled", out var wf) && wf.GetBoolean();
        var citations = ParseStringArray(el, "citations");

        return new AiScoreResult(score, reasoning, suggested, suggestionConfidence, wasFilled, citations);
    }

    private static List<AiTagSuggestion> ParseTagSuggestions(JsonElement root)
    {
        if (!root.TryGetProperty("suggestedTags", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<AiTagSuggestion>();

        var result = new List<AiTagSuggestion>();
        foreach (var item in arr.EnumerateArray())
        {
            string? tag = null;
            int confidence = 0;

            if (item.ValueKind == JsonValueKind.Object)
            {
                tag = item.TryGetProperty("tag", out var t) ? t.GetString() : null;
                confidence = item.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                    ? c.GetInt32()
                    : 0;
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                // graceful fallback if model returns plain strings
                tag = item.GetString();
                confidence = 100;
            }

            if (!string.IsNullOrWhiteSpace(tag) && CanonicalTags.All.Contains(tag))
                result.Add(new AiTagSuggestion(tag, confidence));
        }

        return result;
    }

    private static List<string> ParseStringArray(JsonElement el, string propertyName)
    {
        if (!el.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var result = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            var val = item.GetString();
            if (!string.IsNullOrWhiteSpace(val))
                result.Add(val);
        }

        return result;
    }

    public AiTriageResult? ParseTriageBatchResult(string contentText, string modelUsed)
        => ParseTriageResult(contentText, modelUsed);

    public AiFixResult? ParseFixBatchResult(string contentText, string modelUsed)
        => ParseFixResult(contentText, modelUsed);

    public async Task<BatchSubmitResult> SubmitTriageBatchAsync(
        IEnumerable<(string QuotationId, string Text, string AuthorName, string SourceTitle, string SourceType)> requests)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        var requestList = requests.Select(r => new
        {
            custom_id = r.QuotationId,
            @params = new
            {
                model = _options.Model,
                max_tokens = 256,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = TriageInstructions + "\n\n" + BuildQuoteContext(r.Text, r.AuthorName, null, r.SourceTitle, r.SourceType, null)
                    }
                }
            }
        }).ToList();

        return await SendBatchRequestAsync(requestList);
    }

    public async Task<BatchSubmitResult> SubmitFixBatchAsync(
        IEnumerable<(string QuotationId, string Field, string Text, string AuthorName, string SourceTitle, string SourceType)> requests)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        var requestList = requests.Select(r =>
        {
            var instructions = r.Field switch
            {
                "quote"  => QuoteFixInstructions,
                "author" => AuthorFixInstructions,
                "source" => SourceFixInstructions,
                _        => throw new ArgumentException($"Unknown field: {r.Field}")
            };
            return new
            {
                custom_id = $"{r.QuotationId}:{r.Field}",
                @params = new
                {
                    model = _options.Model,
                    max_tokens = 256,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = instructions + "\n\n" + BuildQuoteContext(r.Text, r.AuthorName, null, r.SourceTitle, r.SourceType, null)
                        }
                    }
                }
            };
        }).ToList();

        return await SendBatchRequestAsync(requestList);
    }

    private async Task<BatchSubmitResult> SendBatchRequestAsync<T>(List<T> requestList)
    {
        var body = JsonSerializer.Serialize(new { requests = requestList });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages/batches");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Batch submit failed {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Anthropic Batch API error {response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var batchId = doc.RootElement.GetProperty("id").GetString()!;
        _logger.LogInformation("Submitted Anthropic batch {BatchId} with {Count} requests", batchId, requestList.Count);
        return new BatchSubmitResult(batchId, requestList.Count);
    }

    public async Task<BatchSubmitResult> SubmitBatchAsync(
        IEnumerable<(string QuotationId, string Text, string AuthorName, string SourceTitle, string SourceType)> requests)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        // Each batch request uses the cached static block + a dynamic per-quote block.
        // Anthropic caches the shared prefix across requests in the same batch,
        // so the ~1,000-token instructions are charged at 10% after the first hit.
        var requestList = requests.Select(r => new
        {
            custom_id = r.QuotationId,
            @params = new
            {
                model = _options.Model,
                max_tokens = _options.MaxTokens,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = StaticInstructions,
                                cache_control = new { type = "ephemeral" }
                            },
                            new
                            {
                                type = "text",
                                text = BuildQuoteContext(r.Text, r.AuthorName, null, r.SourceTitle, r.SourceType, null)
                            }
                        }
                    }
                }
            }
        }).ToList();

        var body = JsonSerializer.Serialize(new { requests = requestList });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages/batches");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Batch submit failed {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Anthropic Batch API error {response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var batchId = doc.RootElement.GetProperty("id").GetString()!;

        _logger.LogInformation("Submitted Anthropic batch {BatchId} with {Count} requests", batchId, requestList.Count);
        return new BatchSubmitResult(batchId, requestList.Count);
    }

    public async Task<BatchStatusResult> GetBatchStatusAsync(string anthropicBatchId)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.anthropic.com/v1/messages/batches/{anthropicBatchId}");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Anthropic Batch status error {response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var processingStatus = root.GetProperty("processing_status").GetString()!;
        var counts = root.GetProperty("request_counts");
        var succeeded = counts.TryGetProperty("succeeded", out var s) ? s.GetInt32() : 0;
        var errored = counts.TryGetProperty("errored", out var e) ? e.GetInt32() : 0;
        var expired = counts.TryGetProperty("expired", out var ex) ? ex.GetInt32() : 0;
        string? resultsUrl = root.TryGetProperty("results_url", out var ru) && ru.ValueKind != JsonValueKind.Null
            ? ru.GetString()
            : null;

        return new BatchStatusResult(anthropicBatchId, processingStatus, succeeded, errored, expired, resultsUrl);
    }

    public async Task<List<BatchMessageResult>> GetBatchResultsAsync(string anthropicBatchId)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.anthropic.com/v1/messages/batches/{anthropicBatchId}/results");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Anthropic Batch results error {response.StatusCode}: {errBody}");
        }

        var results = new List<BatchMessageResult>();
        var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var customId = root.GetProperty("custom_id").GetString()!;
                var result = root.GetProperty("result");
                var resultType = result.GetProperty("type").GetString();

                if (resultType == "succeeded")
                {
                    var message = result.GetProperty("message");
                    var contentText = string.Empty;
                    foreach (var block in message.GetProperty("content").EnumerateArray())
                    {
                        if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                            && block.TryGetProperty("text", out var tx))
                        {
                            contentText += tx.GetString();
                        }
                    }
                    results.Add(new BatchMessageResult(customId, true, contentText, null));
                }
                else
                {
                    var errorType = result.TryGetProperty("error", out var err)
                        ? err.TryGetProperty("type", out var et) ? et.GetString() : resultType
                        : resultType;
                    results.Add(new BatchMessageResult(customId, false, null, errorType));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse batch result line: {Line}", line);
            }
        }

        return results;
    }
}
