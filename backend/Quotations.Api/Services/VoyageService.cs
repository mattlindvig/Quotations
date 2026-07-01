using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;

namespace Quotations.Api.Services;

/// <summary>
/// Embeds text with Voyage AI for Meilisearch hybrid (semantic) search.
/// Returns null when no API key is configured or the call fails, so callers can
/// gracefully fall back to keyword search.
/// </summary>
public class VoyageService
{
    private const string EmbeddingsUrl = "https://api.voyageai.com/v1/embeddings";

    private readonly HttpClient _http;
    private readonly VoyageSettings _settings;
    private readonly ILogger<VoyageService> _logger;

    public VoyageService(HttpClient http, IOptions<VoyageSettings> settings, ILogger<VoyageService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(8);
    }

    public bool Enabled => _settings.Enabled;

    public double SemanticRatio => _settings.SemanticRatio;

    /// <summary>Embed a single search query. Returns null if disabled or on failure.</summary>
    public async Task<float[]?> EmbedQueryAsync(string text)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, EmbeddingsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = JsonContent.Create(new VoyageRequest
            {
                Input = new[] { text },
                Model = _settings.Model,
                InputType = "query",
                OutputDimension = _settings.Dimensions,
            });

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Voyage embed failed: {Status}", response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<VoyageResponse>();
            return body?.Data?.FirstOrDefault()?.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voyage embed call threw — falling back to keyword search");
            return null;
        }
    }

    private sealed class VoyageRequest
    {
        [JsonPropertyName("input")]
        public string[] Input { get; set; } = Array.Empty<string>();

        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("input_type")]
        public string InputType { get; set; } = "query";

        [JsonPropertyName("output_dimension")]
        public int OutputDimension { get; set; }
    }

    private sealed class VoyageResponse
    {
        [JsonPropertyName("data")]
        public List<VoyageDatum>? Data { get; set; }
    }

    private sealed class VoyageDatum
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
