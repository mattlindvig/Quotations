using Meilisearch;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using System.Text.Json.Serialization;

namespace Quotations.Api.Services;

public class MeilisearchService
{
    private readonly MeilisearchSettings _settings;
    private readonly MeilisearchClient _client;
    private const string IndexName = "quotations";
    private const string EmbedderName = "default";

    public bool Enabled => _settings.Enabled;

    public MeilisearchService(IOptions<MeilisearchSettings> opts)
    {
        _settings = opts.Value;
        var http = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri(_settings.Url.TrimEnd('/') + '/'),
            Timeout = TimeSpan.FromSeconds(8),
        };
        _client = new MeilisearchClient(http, _settings.ApiKey);
    }

    public async Task<(List<MeiliQuotationDoc> Hits, long TotalCount)> SearchAsync(
        string query,
        int page,
        int pageSize,
        string status = "Approved",
        string? authorName = null,
        string? sourceType = null,
        string? sourceTitle = null,
        List<string>? tags = null,
        int? yearFrom = null,
        int? yearTo = null,
        string? sortBy = null,
        bool verifiedOnly = false,
        IEnumerable<float>? vector = null,
        double? semanticRatio = null)
    {
        var filters = new List<string> { $"status = \"{Escape(status)}\"" };
        if (!string.IsNullOrEmpty(authorName))
            filters.Add($"authorName = \"{Escape(authorName)}\"");
        if (!string.IsNullOrEmpty(sourceType))
            filters.Add($"sourceType = \"{Escape(sourceType)}\"");
        if (!string.IsNullOrEmpty(sourceTitle))
            filters.Add($"sourceTitle = \"{Escape(sourceTitle)}\"");
        if (tags != null)
            foreach (var tag in tags)
                filters.Add($"tags = \"{Escape(tag)}\"");
        if (yearFrom.HasValue)
            filters.Add($"year >= {yearFrom}");
        if (yearTo.HasValue)
            filters.Add($"year <= {yearTo}");
        if (verifiedOnly)
            filters.Add("isVerified = true");

        var sq = new SearchQuery
        {
            Offset = (page - 1) * pageSize,
            Limit = pageSize,
            Filter = string.Join(" AND ", filters),
        };

        // Hybrid (semantic) search — only when the caller supplied a query embedding.
        // Falls back to plain keyword ranking otherwise.
        if (vector != null)
        {
            sq.Vector = vector.Select(v => (double)v).ToArray();
            sq.Hybrid = new HybridSearch
            {
                Embedder = EmbedderName,
                SemanticRatio = (float)(semanticRatio ?? 0.5),
            };
        }

        // Sort only for filter-browse (empty query); text search uses relevance ranking
        if (string.IsNullOrWhiteSpace(query) && sortBy != null)
        {
            sq.Sort = sortBy switch
            {
                "oldest" => new[] { "submittedAt:asc" },
                "author" => new[] { "authorName:asc" },
                "year"   => new[] { "year:desc" },
                _        => new[] { "submittedAt:desc" },
            };
        }

        var result = await _client.Index(IndexName).SearchAsync<MeiliQuotationDoc>(query, sq);
        var hits = result.Hits.Where(h => !string.IsNullOrEmpty(h.Id)).ToList();
        var total = (result as Meilisearch.SearchResult<MeiliQuotationDoc>)?.EstimatedTotalHits ?? (long)hits.Count;
        return (hits, total);
    }

    public async Task IndexDocumentsAsync(IEnumerable<MeiliQuotationDoc> docs)
    {
        await _client.Index(IndexName).AddDocumentsAsync(docs, primaryKey: "id");
    }

    public async Task DeleteDocumentAsync(string id)
    {
        await _client.Index(IndexName).DeleteOneDocumentAsync(id);
    }

    public async Task ConfigureIndexAsync()
    {
        var index = _client.Index(IndexName);
        await index.UpdateSearchableAttributesAsync(new[] { "text", "authorName", "sourceTitle" });
        await index.UpdateFilterableAttributesAsync(new[] { "status", "sourceType", "tags", "year", "authorName", "sourceTitle", "isVerified" });
        await index.UpdateSortableAttributesAsync(new[] { "submittedAt", "authorName", "year" });
        // Note: the "default" userProvided embedder for semantic search is configured by
        // tools/embed_quotes.py, which also backfills the per-document vectors.
    }

    private static string Escape(string val) => val.Replace("\\", "\\\\").Replace("\"", "\\\"");

}

public class MeiliQuotationDoc
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("authorName")]
    public string AuthorName { get; set; } = "";

    [JsonPropertyName("sourceTitle")]
    public string SourceTitle { get; set; } = "";

    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("submittedAt")]
    public long SubmittedAt { get; set; }

    /// <summary>True when AI review ran and judged the attribution likely authentic.</summary>
    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }
}
