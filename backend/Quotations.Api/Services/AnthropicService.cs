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

public record BatchSubmitResult(string AnthropicBatchId, int RequestCount);
public record BatchStatusResult(string AnthropicBatchId, string ProcessingStatus, int Succeeded, int Errored, int Expired, string? ResultsUrl);
public record BatchMessageResult(string CustomId, bool Succeeded, string? ContentText, string? ErrorType);

/// <summary>
/// Result of a lean single-pass review: tags to merge and field corrections (null = no change).
/// </summary>
public record LeanReviewResult(
    List<string> Tags,
    string? Author,
    string? Source,
    string? SourceType,
    string? Text,
    bool Reject,
    string ModelUsed);

public interface IAnthropicService
{
    /// <summary>
    /// Single-pass lean review: returns tags + optional field corrections.
    /// </summary>
    Task<LeanReviewResult?> LeanReviewAsync(string text, string authorName, string sourceTitle, string sourceType, IEnumerable<string> existingTags);

    /// <summary>
    /// Parse a lean review result from a batch response content string.
    /// </summary>
    LeanReviewResult? ParseLeanBatchResult(string contentText, string modelUsed);

    /// <summary>
    /// Submit a lean review batch to the Anthropic Batch API (50% cost discount).
    /// </summary>
    Task<BatchSubmitResult> SubmitLeanBatchAsync(
        IEnumerable<(string QuotationId, string Text, string AuthorName, string SourceTitle, string SourceType, IEnumerable<string> ExistingTags)> requests);

    Task<BatchStatusResult> GetBatchStatusAsync(string anthropicBatchId);
    Task<List<BatchMessageResult>> GetBatchResultsAsync(string anthropicBatchId);
}

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

    public AnthropicService(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptions<AiReviewOptions> options,
        ILogger<AnthropicService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _apiKey = (configuration["Anthropic:ApiKey"] ?? string.Empty).Trim();
    }

    // Built once — shared across all requests to take advantage of prompt caching.
    private static readonly string LeanPrompt = BuildLeanPrompt();

    private static string BuildLeanPrompt()
    {
        var thematicTags = string.Join(", ", CanonicalTags.All);
        var bannedTags = string.Join(", ", CanonicalTags.BannedTags.OrderBy(t => t).Take(20));
        return
            "You are a quotation editor. Given a quote, author, source, and current tags:\n\n" +
            "1. Tags — return the COMPLETE FINAL tag list (fully replaces existing tags):\n" +
            "   - Remove useless generic tags such as: " + bannedTags + ", and similar.\n" +
            "   - Remove tags that duplicate the author's name or source title (those are shown as separate fields).\n" +
            "   - Remove navigation garbage: anything containing '>', '{', '}', '|', or MediaWiki template syntax.\n" +
            "   - Consolidate near-duplicates to the simpler form (e.g. keep \"mass-effect\" not \"mass-effect-video-game\").\n" +
            "   - Keep specific accurate tags (character names, franchise names, work titles).\n" +
            "   - Add relevant thematic tags from this list where appropriate: " + thematicTags + "\n" +
            "   - Aim for 2-8 meaningful tags total.\n\n" +
            "2. Fix the author if blank, \"Unknown\", \"Anonymous\", a placeholder, or parsing garbage (URL, HTML, Wikipedia article title, chapter heading, episode name, or any non-person text).\n" +
            "   → Always return your best identification — even at lower confidence. Return null only if the existing value is already a correct person name.\n\n" +
            "3. Fix the source title if blank, \"Unknown\", \"Other\", a placeholder, or parsing garbage.\n" +
            "   → Always return your best identification — even at lower confidence. Return null only if the existing value is already correct.\n\n" +
            "4. Fix the source type if it is wrong or \"Other\" and you can determine the real type.\n" +
            "   Available types: Book, Movie, Television, Speech, Interview, Poem, Song, Play, Musical,\n" +
            "   VideoGame, Comic, Article, Letter, Podcast, Documentary, Scripture, Proverb, Memoir, Standup, Organization, Other.\n" +
            "   Return null if the current type is already correct.\n\n" +
            "5. If the quote text has a clear wording error vs. the widely-known canonical version — provide the corrected text.\n\n" +
            "6. Set reject=true if the text is NOT a real quotation (episode list, file metadata, navigation text, raw HTML, etc.).\n\n" +
            "Rules: Set a field to null if no change is needed. Only correct text if highly confident.\n\n" +
            "Respond ONLY with valid JSON:\n" +
            "{\"tags\":[\"tag1\"],\"author\":null,\"source\":null,\"sourceType\":null,\"text\":null,\"reject\":false}";
    }

    private static string BuildQuoteContext(
        string text, string authorName, string sourceTitle, string sourceType, IEnumerable<string> existingTags)
    {
        var tagList = existingTags.Any() ? string.Join(", ", existingTags) : "(none)";
        return $"Quotation text: \"{text}\"\nAttributed to: {authorName}\nSource: {sourceTitle} ({sourceType})\nCurrent tags: {tagList}";
    }

    public async Task<LeanReviewResult?> LeanReviewAsync(
        string text, string authorName, string sourceTitle, string sourceType, IEnumerable<string> existingTags)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
        {
            _logger.LogWarning("Anthropic API key not configured — skipping lean review");
            return null;
        }

        using var lease = await RateLimiter.AcquireAsync(permitCount: 1);
        if (!lease.IsAcquired)
            throw new InvalidOperationException("Anthropic rate-limit queue is full — too many requests queued");

        var requestNode = new JsonObject
        {
            ["model"] = _options.Model,
            ["max_tokens"] = 200,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = LeanPrompt,
                            ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
                        },
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = BuildQuoteContext(text, authorName, sourceTitle, sourceType, existingTags)
                        }
                    }
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

        return ParseLeanResult(contentText, modelUsed);
    }

    public LeanReviewResult? ParseLeanBatchResult(string contentText, string modelUsed)
        => ParseLeanResult(contentText, modelUsed);

    private LeanReviewResult? ParseLeanResult(string contentText, string modelUsed)
    {
        try
        {
            var start = contentText.IndexOf('{');
            var end = contentText.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            using var doc = JsonDocument.Parse(contentText[start..(end + 1)]);
            var root = doc.RootElement;

            var tags = new List<string>();
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tagsEl.EnumerateArray())
                {
                    var tag = item.GetString()?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(tag) && !CanonicalTags.BannedTags.Contains(tag))
                        tags.Add(tag);
                }
            }

            string? author     = GetNullableString(root, "author");
            string? source     = GetNullableString(root, "source");
            string? sourceType = GetNullableString(root, "sourceType");
            string? text       = GetNullableString(root, "text");
            bool reject = root.TryGetProperty("reject", out var rejectEl)
                && rejectEl.ValueKind == JsonValueKind.True;

            return new LeanReviewResult(tags, author, source, sourceType, text, reject, modelUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse lean review response: {Content}", contentText);
            return null;
        }
    }

    private static string? GetNullableString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Null) return null;
        var val = el.GetString();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    public async Task<BatchSubmitResult> SubmitLeanBatchAsync(
        IEnumerable<(string QuotationId, string Text, string AuthorName, string SourceTitle, string SourceType, IEnumerable<string> ExistingTags)> requests)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        var requestList = requests.Select(r => new
        {
            custom_id = r.QuotationId,
            @params = new
            {
                model = _options.Model,
                max_tokens = 200,
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
                                text = LeanPrompt,
                                cache_control = new { type = "ephemeral" }
                            },
                            new
                            {
                                type = "text",
                                text = BuildQuoteContext(r.Text, r.AuthorName, r.SourceTitle, r.SourceType, r.ExistingTags)
                            }
                        }
                    }
                }
            }
        }).ToList();

        return await SendBatchRequestAsync(requestList);
    }

    public async Task<BatchStatusResult> GetBatchStatusAsync(string anthropicBatchId)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.anthropic.com/v1/messages/batches/{anthropicBatchId}");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Anthropic Batch status error {response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var counts = root.GetProperty("request_counts");
        return new BatchStatusResult(
            anthropicBatchId,
            root.GetProperty("processing_status").GetString()!,
            counts.TryGetProperty("succeeded", out var s) ? s.GetInt32() : 0,
            counts.TryGetProperty("errored",   out var e) ? e.GetInt32() : 0,
            counts.TryGetProperty("expired",   out var x) ? x.GetInt32() : 0,
            root.TryGetProperty("results_url", out var ru) && ru.ValueKind != JsonValueKind.Null
                ? ru.GetString() : null);
    }

    public async Task<List<BatchMessageResult>> GetBatchResultsAsync(string anthropicBatchId)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
            throw new InvalidOperationException("Anthropic API key not configured");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.anthropic.com/v1/messages/batches/{anthropicBatchId}/results");
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
                            contentText += tx.GetString();
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

    private async Task<BatchSubmitResult> SendBatchRequestAsync<T>(List<T> requestList)
    {
        var body = JsonSerializer.Serialize(new { requests = requestList });

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.anthropic.com/v1/messages/batches");
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

    private async Task<string> SendWithRetryAsync(string json)
    {
        const int MaxRetries = 4;
        var delay = TimeSpan.FromSeconds(65);

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

                if (response.Headers.RetryAfter?.Delta is { } retryAfterDelta)
                    delay = retryAfterDelta + TimeSpan.FromSeconds(2);

                _logger.LogWarning(
                    "Anthropic rate limit hit (attempt {Attempt}/{Max}), waiting {Seconds}s",
                    attempt + 1, MaxRetries, (int)delay.TotalSeconds);
                await Task.Delay(delay);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
                throw new InvalidOperationException($"Anthropic API error {response.StatusCode}: {body}");
            }

            return body;
        }

        throw new InvalidOperationException("Unreachable");
    }
}
