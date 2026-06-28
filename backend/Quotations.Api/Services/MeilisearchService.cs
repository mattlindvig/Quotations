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

    public bool Enabled => _settings.Enabled;

    public MeilisearchService(IOptions<MeilisearchSettings> opts)
    {
        _settings = opts.Value;
        var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client = new MeilisearchClient(_settings.Url, _settings.ApiKey, http);
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
        string? sortBy = null)
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

        var sq = new SearchQuery
        {
            Offset = (page - 1) * pageSize,
            Limit = pageSize,
            Filter = string.Join(" AND ", filters),
        };

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
        await index.UpdateFilterableAttributesAsync(new[] { "status", "sourceType", "tags", "year", "authorName", "sourceTitle" });
        await index.UpdateSortableAttributesAsync(new[] { "submittedAt", "authorName", "year" });
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
}
