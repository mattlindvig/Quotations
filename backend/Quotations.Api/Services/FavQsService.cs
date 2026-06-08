using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;

namespace Quotations.Api.Services;

public class FavQsService
{
    private readonly HttpClient _http;
    private readonly IQuotationRepository _quotationRepo;
    private readonly FavQsSyncOptions _options;
    private readonly ILogger<FavQsService> _logger;

    private const string ApiBase = "https://favqs.com/api";

    public FavQsService(
        HttpClient http,
        IQuotationRepository quotationRepo,
        IOptions<FavQsSyncOptions> options,
        ILogger<FavQsService> logger)
    {
        _http = http;
        _quotationRepo = quotationRepo;
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<(int inserted, int skipped)> RunSyncAsync(
        FavQsSyncRecord record,
        [EnumeratorCancellation] CancellationToken ct,
        int startPage = 1)
    {
        var page = startPage;

        while (true)
        {
            if (ct.IsCancellationRequested) yield break;

            var response = await FetchPageAsync(page, ct);
            if (response is null) break;

            var quotes = response.Quotes
                .Where(q => !q.Dialogue && !q.Private && q.Body.Length >= _options.MinQuoteLength)
                .ToList();

            record.ResumePage = page;

            if (quotes.Count > 0)
            {
                var docs = quotes.Select(q => new Quotation
                {
                    Text = q.Body.Trim(),
                    TextHash = Quotation.ComputeTextHash(q.Body.Trim()),
                    Author = new AuthorReference { Name = NormaliseAuthor(q.Author) },
                    Source = new SourceReference { Type = SourceType.Other },
                    Tags = q.Tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).ToList(),
                    Status = QuotationStatus.Approved,
                    SubmittedAt = DateTime.UtcNow,
                    ReviewedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    AiReview = new AiReview { Status = AiReviewStatus.NotReviewed }
                }).ToList();

                var (inserted, skipped) = await _quotationRepo.BulkInsertAsync(docs);
                record.QuotesInserted += inserted;
                record.QuotesSkipped += skipped;
                yield return (inserted, skipped);
            }
            else
            {
                yield return (0, 0);
            }

            record.PagesProcessed++;

            if (response.LastPage) break;

            page++;
            await Task.Delay(_options.DelayBetweenRequestsMs, ct);
        }
    }

    private async Task<FavQsPageResponse?> FetchPageAsync(int page, CancellationToken ct)
    {
        try
        {
            var url = $"{ApiBase}/quotes?page={page}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Token token=\"{_options.ApiKey}\"");

            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<FavQsPageResponse>(json, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FavQs API request failed for page {Page}", page);
            return null;
        }
    }

    private static string NormaliseAuthor(string author) =>
        string.IsNullOrWhiteSpace(author) || author.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            ? "Unknown"
            : author.Trim();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

// ── FavQs API response models ──────────────────────────────────────────────────

internal class FavQsPageResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("last_page")]
    public bool LastPage { get; set; }

    [JsonPropertyName("quotes")]
    public List<FavQsQuote> Quotes { get; set; } = new();
}

internal class FavQsQuote
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("dialogue")]
    public bool Dialogue { get; set; }

    [JsonPropertyName("private")]
    public bool Private { get; set; }
}
