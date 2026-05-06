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

public interface IAnthropicService
{
    Task<AiAnalysisResult?> AnalyzeQuotationAsync(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear,
        bool useWebSearch);

    AiAnalysisRequestPreview BuildRequestPreview(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear,
        bool useWebSearch);
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

public class AnthropicService : IAnthropicService
{
    // Shared across all instances — 50 requests/min, queues callers rather than rejecting them
    private static readonly TokenBucketRateLimiter RateLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 50,
        TokensPerPeriod = 50,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 500,
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
        bool useWebSearch)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
        {
            _logger.LogWarning("Anthropic API key not configured — skipping AI analysis");
            return null;
        }

        using var lease = await RateLimiter.AcquireAsync(permitCount: 1);
        if (!lease.IsAcquired)
            throw new InvalidOperationException("Anthropic rate-limit queue is full — too many requests queued");

        var prompt = BuildPrompt(text, authorName, authorLifespan, sourceTitle, sourceType, sourceYear);

        // Maintain full message history to support pause_turn continuation
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["content"] = prompt }
        };

        var toolsJson = useWebSearch
            ? """[{"type":"web_search_20250305","name":"web_search","max_uses":5}]"""
            : "[]";
        string contentText = string.Empty;
        string modelUsed = _options.Model;

        for (int iteration = 0; iteration < 15; iteration++)
        {
            var requestNode = new JsonObject
            {
                ["model"] = _options.Model,
                ["max_tokens"] = _options.MaxTokens,
                ["messages"] = JsonNode.Parse(messages.ToJsonString()),
                ["tools"] = JsonNode.Parse(toolsJson)
            };

            var responseBody = await SendWithRetryAsync(requestNode.ToJsonString());

            using var doc = JsonDocument.Parse(responseBody);
            var stopReason = doc.RootElement.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : null;
            modelUsed = doc.RootElement.TryGetProperty("model", out var m) ? m.GetString() ?? _options.Model : _options.Model;

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

    private async Task<string> SendWithRetryAsync(string json)
    {
        const int MaxRetries = 4;
        var delay = TimeSpan.FromSeconds(65); // safe default: just past the 1-min TPM window

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", ApiVersion);
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

    private static string BuildPrompt(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear)
    {
        var lifespanPart = !string.IsNullOrEmpty(authorLifespan) ? $" ({authorLifespan})" : string.Empty;
        var yearPart = sourceYear.HasValue ? $", {sourceYear}" : string.Empty;
        var tagList = string.Join(", ", CanonicalTags.All);

        return
            "You are a quotation accuracy expert with access to web search. Analyze the following quotation — search the web as needed to verify facts — and respond ONLY with valid JSON.\n\n" +
            $"Quotation text: \"{text}\"\n" +
            $"Attributed to: {authorName}{lifespanPart}\n" +
            $"Source: {sourceTitle} ({sourceType}{yearPart})\n\n" +
            "Evaluate three dimensions and score each 1–10:\n" +
            "  quoteAccuracy      — Is this the exact wording typically associated with this person?\n" +
            "  attributionAccuracy — Did this specific person actually say or write this?\n" +
            "  sourceAccuracy     — Is this source title and type correct for this quote?\n\n" +
            "SUGGESTION RULES (apply to all three dimensions):\n" +
            "  - Score >= 8: set suggestedValue to null and omit suggestionConfidence (no fix needed).\n" +
            "  - Score 5–7: provide suggestedValue if you know a better/corrected version; set suggestionConfidence (0–100) to your confidence that the suggestion is more accurate than the original.\n" +
            "  - Score <= 4: always provide suggestedValue with the correct version; set suggestionConfidence (0–100) accordingly.\n" +
            "  - Set wasAiFilled to true only when the original field was blank/unknown and you are supplying a known value.\n\n" +
            "CITATION RULES (apply to each dimension independently):\n" +
            "  - Provide citations only for claims you can verify.\n" +
            "  - High confidence (score 8–10): cite as specifically as possible — book title, edition, page, paragraph if known.\n" +
            "  - Medium confidence (score 5–7): cite the reference but omit page/paragraph details.\n" +
            "  - Low confidence (score 1–4): omit citations entirely (empty array) — do not guess.\n\n" +
            "TAG RULES:\n" +
            $"  Select 1–5 tags from this exact list that best describe the quotation's themes: {tagList}\n" +
            "  Return only tags from that list, no others.\n" +
            "  For each tag include a confidence (0–100): your confidence that this tag genuinely applies to the quotation.\n\n" +
            "AUTHENTICITY METADATA:\n" +
            "  - isLikelyAuthentic: true if you believe this quotation is genuinely from the attributed author; false if it is likely misattributed or apocryphal.\n" +
            "  - authenticityReasoning: one or two sentences explaining your authenticity assessment.\n" +
            "  - approximateEra: a short phrase placing the quotation historically (e.g. \"Ancient Greece, ~400 BCE\", \"Victorian era, 1880s\", \"20th century, 1940s\").\n" +
            "  - knownVariants: an array of alternate wordings for this quotation that are commonly found in the wild (empty array if none known).\n\n" +
            "Respond with exactly this JSON (no extra text):\n" +
            "{\n" +
            "  \"quoteAccuracy\": {\n" +
            "    \"score\": 0,\n" +
            "    \"reasoning\": \"\",\n" +
            "    \"suggestedValue\": null,\n" +
            "    \"suggestionConfidence\": null,\n" +
            "    \"wasAiFilled\": false,\n" +
            "    \"citations\": []\n" +
            "  },\n" +
            "  \"attributionAccuracy\": {\n" +
            "    \"score\": 0,\n" +
            "    \"reasoning\": \"\",\n" +
            "    \"suggestedValue\": null,\n" +
            "    \"suggestionConfidence\": null,\n" +
            "    \"wasAiFilled\": false,\n" +
            "    \"citations\": []\n" +
            "  },\n" +
            "  \"sourceAccuracy\": {\n" +
            "    \"score\": 0,\n" +
            "    \"reasoning\": \"\",\n" +
            "    \"suggestedValue\": null,\n" +
            "    \"suggestionConfidence\": null,\n" +
            "    \"wasAiFilled\": false,\n" +
            "    \"citations\": []\n" +
            "  },\n" +
            "  \"suggestedTags\": [{\"tag\": \"\", \"confidence\": 0}],\n" +
            "  \"summary\": \"\",\n" +
            "  \"isLikelyAuthentic\": true,\n" +
            "  \"authenticityReasoning\": \"\",\n" +
            "  \"approximateEra\": \"\",\n" +
            "  \"knownVariants\": []\n" +
            "}";
    }

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
}
