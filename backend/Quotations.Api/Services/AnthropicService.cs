using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quotations.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        int? sourceYear);

    AiAnalysisRequestPreview BuildRequestPreview(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear);
}

public record AiScoreResult(
    int Score,
    string Reasoning,
    string? SuggestedValue,
    bool WasAiFilled,
    List<string> Citations);

public record AiAnalysisResult(
    AiScoreResult QuoteAccuracy,
    AiScoreResult AttributionAccuracy,
    AiScoreResult SourceAccuracy,
    string Summary,
    List<string> SuggestedTags,
    string ModelUsed);

public class AnthropicService : IAnthropicService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicService> _logger;
    private readonly string _apiKey;
    private const string Model = "claude-haiku-4-5-20251001";
    private const string ApiVersion = "2023-06-01";


    public AnthropicService(HttpClient httpClient, IConfiguration configuration, ILogger<AnthropicService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = (configuration["Anthropic:ApiKey"] ?? string.Empty).Trim();
    }

    public AiAnalysisRequestPreview BuildRequestPreview(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear)
    {
        var prompt = BuildPrompt(text, authorName, authorLifespan, sourceTitle, sourceType, sourceYear);
        var requestBody = new
        {
            model = Model,
            max_tokens = 1536,
            messages = new[] { new { role = "user", content = prompt } }
        };
        var requestJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true });
        return new AiAnalysisRequestPreview(Model, 1536, prompt, requestJson);
    }

    public async Task<AiAnalysisResult?> AnalyzeQuotationAsync(
        string text,
        string authorName,
        string? authorLifespan,
        string sourceTitle,
        string sourceType,
        int? sourceYear)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "REPLACE_WITH_ANTHROPIC_API_KEY")
        {
            _logger.LogWarning("Anthropic API key not configured — skipping AI analysis");
            return null;
        }

        var prompt = BuildPrompt(text, authorName, authorLifespan, sourceTitle, sourceType, sourceYear);
        var requestBody = new
        {
            model = Model,
            max_tokens = 1536,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);

        // Buffer the content before reading so the body is available regardless of the handler pipeline
        await response.Content.LoadIntoBufferAsync();
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Anthropic API returned {StatusCode} (Content-Type: {ContentType}, Length: {Length}): {Body}",
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.ToString() ?? "unknown",
                response.Content.Headers.ContentLength?.ToString() ?? "unknown",
                string.IsNullOrWhiteSpace(responseBody) ? "<empty body>" : responseBody);
            throw new InvalidOperationException($"Anthropic API error {response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var contentText = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var modelUsed = doc.RootElement.GetProperty("model").GetString() ?? Model;

        return ParseAnalysisResult(contentText, modelUsed);
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
            "You are a quotation accuracy expert. Analyze the following quotation and respond ONLY with valid JSON.\n\n" +
            $"Quotation text: \"{text}\"\n" +
            $"Attributed to: {authorName}{lifespanPart}\n" +
            $"Source: {sourceTitle} ({sourceType}{yearPart})\n\n" +
            "Evaluate three dimensions and score each 1–10:\n" +
            "  quoteAccuracy      — Is this the exact wording typically associated with this person?\n" +
            "  attributionAccuracy — Did this specific person actually say or write this?\n" +
            "  sourceAccuracy     — Is this source title and type correct for this quote?\n\n" +
            "SUGGESTION RULES (apply to all three dimensions):\n" +
            "  - Score >= 8: set suggestedValue to null (no fix needed).\n" +
            "  - Score 5–7: provide suggestedValue if you know a better/corrected version.\n" +
            "  - Score <= 4: always provide suggestedValue with the correct version, or explain why it cannot be determined.\n" +
            "  - Set wasAiFilled to true only when the original field was blank/unknown and you are supplying a known value.\n\n" +
            "CITATION RULES (apply to each dimension independently):\n" +
            "  - Provide citations only for claims you can verify.\n" +
            "  - High confidence (score 8–10): cite as specifically as possible — book title, edition, page, paragraph if known.\n" +
            "  - Medium confidence (score 5–7): cite the reference but omit page/paragraph details.\n" +
            "  - Low confidence (score 1–4): omit citations entirely (empty array) — do not guess.\n\n" +
            "TAG RULES:\n" +
            $"  Select 1–5 tags from this exact list that best describe the quotation's themes: {tagList}\n" +
            "  Return only tags from that list, no others.\n\n" +
            "Respond with exactly this JSON (no extra text):\n" +
            "{\n" +
            "  \"quoteAccuracy\": {\n" +
            "    \"score\": 0,\n" +
            "    \"reasoning\": \"\",\n" +
            "    \"suggestedValue\": null,\n" +
            "    \"wasAiFilled\": false,\n" +
            "    \"citations\": []\n" +
            "  },\n" +
            "  \"attributionAccuracy\": {\n" +
            "    \"score\": 0,\n" +
            "    \"reasoning\": \"\",\n" +
            "    \"suggestedValue\": null,\n" +
            "    \"wasAiFilled\": false,\n" +
            "    \"citations\": []\n" +
            "  },\n" +
            "  \"sourceAccuracy\": {\n" +
            "    \"score\": 0,\n" +
            "    \"reasoning\": \"\",\n" +
            "    \"suggestedValue\": null,\n" +
            "    \"wasAiFilled\": false,\n" +
            "    \"citations\": []\n" +
            "  },\n" +
            "  \"suggestedTags\": [],\n" +
            "  \"summary\": \"\"\n" +
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

            var suggestedTags = ParseStringArray(root, "suggestedTags")
                .Where(t => CanonicalTags.All.Contains(t))
                .ToList();

            var summary = root.TryGetProperty("summary", out var summaryEl)
                ? summaryEl.GetString() ?? string.Empty
                : string.Empty;

            return new AiAnalysisResult(
                ParseScore(root, "quoteAccuracy"),
                ParseScore(root, "attributionAccuracy"),
                ParseScore(root, "sourceAccuracy"),
                summary,
                suggestedTags,
                modelUsed);
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
            return new AiScoreResult(0, "Not evaluated", null, false, new List<string>());

        var score = el.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
        var reasoning = el.TryGetProperty("reasoning", out var r) ? r.GetString() ?? string.Empty : string.Empty;
        var suggested = el.TryGetProperty("suggestedValue", out var sv) && sv.ValueKind != JsonValueKind.Null
            ? sv.GetString()
            : null;
        var wasFilled = el.TryGetProperty("wasAiFilled", out var wf) && wf.GetBoolean();
        var citations = ParseStringArray(el, "citations");

        return new AiScoreResult(score, reasoning, suggested, wasFilled, citations);
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
