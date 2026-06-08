using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Quotations.Api.Configuration;
using Quotations.Api.Models;
using Quotations.Api.Repositories;

namespace Quotations.Api.Services;

public record ParsedQuote(string Text, string AuthorName, string SourceTitle, SourceType SourceType, int? SourceYear, List<string> Tags);

public class WikiquoteService
{
    private readonly HttpClient _http;
    private readonly IQuotationRepository _quotationRepo;
    private readonly WikiquoteSyncOptions _options;
    private readonly ILogger<WikiquoteService> _logger;

    private const string ApiBase = "https://en.wikiquote.org/w/api.php";

    public WikiquoteService(
        HttpClient http,
        IQuotationRepository quotationRepo,
        IOptions<WikiquoteSyncOptions> options,
        ILogger<WikiquoteService> logger)
    {
        _http = http;
        _quotationRepo = quotationRepo;
        _options = options.Value;
        _logger = logger;
    }

    // ── Full sync ──────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<(int inserted, int skipped)> RunFullSyncAsync(
        WikiquoteSyncRecord record, CancellationToken ct, string? resumeToken = null)
    {
        string? continueToken = resumeToken;

        do
        {
            var url = $"{ApiBase}?action=query&list=allpages&apnamespace=0&aplimit=50&format=json";
            if (continueToken != null)
                url += $"&apcontinue={Uri.EscapeDataString(continueToken)}";

            var response = await FetchAsync<WikiquoteAllPagesResponse>(url, ct);
            if (response is null) break;

            var pages = response.Query.AllPages.Select(p => p.Title).ToList();
            continueToken = response.Continue?.ApContinue;
            record.ContinueToken = continueToken; // save so a restart can resume this batch

            foreach (var title in pages)
            {
                if (ct.IsCancellationRequested) yield break;
                if (_options.MaxPagesPerRun > 0 && record.PagesProcessed >= _options.MaxPagesPerRun) yield break;

                var result = await ProcessPageAsync(title, ct);
                record.PagesProcessed++;
                record.QuotesInserted += result.inserted;
                record.QuotesSkipped += result.skipped;

                yield return result;

                await Task.Delay(_options.DelayBetweenRequestsMs, ct);
            }

        } while (continueToken != null);
    }

    // ── Delta sync ─────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<(int inserted, int skipped)> RunDeltaSyncAsync(
        DateTime since, WikiquoteSyncRecord record, CancellationToken ct)
    {
        string? continueToken = null;
        var sinceStr = since.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        do
        {
            var url = $"{ApiBase}?action=query&list=recentchanges&rcnamespace=0&rctype=edit|new"
                    + $"&rcend={Uri.EscapeDataString(sinceStr)}&rclimit=50&rcprop=title&format=json";
            if (continueToken != null)
                url += $"&rccontinue={Uri.EscapeDataString(continueToken)}";

            var response = await FetchAsync<WikiquoteRecentChangesResponse>(url, ct);
            if (response is null) break;

            var titles = response.Query.RecentChanges.Select(c => c.Title).Distinct().ToList();
            continueToken = response.Continue?.RcContinue;

            foreach (var title in titles)
            {
                if (ct.IsCancellationRequested) yield break;

                var result = await ProcessPageAsync(title, ct);
                record.PagesProcessed++;
                record.QuotesInserted += result.inserted;
                record.QuotesSkipped += result.skipped;

                yield return result;

                await Task.Delay(_options.DelayBetweenRequestsMs, ct);
            }

        } while (continueToken != null);
    }

    // ── Page processing ────────────────────────────────────────────────────────

    private async Task<(int inserted, int skipped)> ProcessPageAsync(string title, CancellationToken ct)
    {
        try
        {
            var (wikitext, categories) = await FetchPageAsync(title, ct);
            if (wikitext is null) return (0, 0);

            var sourceType = DetectSourceType(title, categories);
            var isPersonPage = IsPersonPage(categories);
            var quotes = ParseWikitext(wikitext, title, isPersonPage, sourceType);

            if (quotes.Count == 0) return (0, 0);

            var newDocs = quotes
                .Where(q => q.Text.Length >= _options.MinQuoteLength)
                .Select(q => new Quotation
                {
                    Text = q.Text,
                    TextHash = Quotation.ComputeTextHash(q.Text),
                    Author = new AuthorReference { Name = q.AuthorName },
                    Source = new SourceReference { Title = q.SourceTitle, Type = q.SourceType, Year = q.SourceYear },
                    Tags = q.Tags,
                    Status = QuotationStatus.Approved,
                    SubmittedAt = DateTime.UtcNow,
                    ReviewedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    AiReview = new AiReview { Status = AiReviewStatus.NotReviewed }
                })
                .ToList();

            if (newDocs.Count == 0) return (0, quotes.Count);

            // Let MongoDB reject duplicates via the unique text index — no pre-query needed
            var (inserted, skipped) = await _quotationRepo.BulkInsertAsync(newDocs);
            return (inserted, skipped);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Wikiquote page: {Title}", title);
            return (0, 0);
        }
    }

    // ── Wikitext parser ────────────────────────────────────────────────────────

    private List<ParsedQuote> ParseWikitext(string wikitext, string pageTitle, bool isPersonPage, SourceType sourceType)
    {
        var results = new List<ParsedQuote>();
        var lines = wikitext.Split('\n');

        var authorName = isPersonPage ? CleanTitle(pageTitle) : string.Empty;
        var sourceTitle = isPersonPage ? string.Empty : CleanTitle(pageTitle);

        string currentSection = string.Empty;
        string currentSubsection = string.Empty;
        string? pendingQuote = null;
        string? pendingCharacter = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            // H3 subsection (===Title===)
            if (line.StartsWith("===") && line.EndsWith("==="))
            {
                FlushPending(ref pendingQuote, ref pendingCharacter, authorName, sourceTitle, currentSubsection, pageTitle, sourceType, isPersonPage, results);
                currentSubsection = StripSectionMarkers(line, 3);
                continue;
            }

            // H2 section (==Title==)
            if (line.StartsWith("==") && line.EndsWith("==") && !line.StartsWith("==="))
            {
                FlushPending(ref pendingQuote, ref pendingCharacter, authorName, sourceTitle, currentSubsection, pageTitle, sourceType, isPersonPage, results);
                currentSection = StripSectionMarkers(line, 2);
                currentSubsection = string.Empty;
                continue;
            }

            if (IsMeta(currentSection)) continue;

            // Attribution line (**): belongs to the pending quote
            if (line.StartsWith("**") && !line.StartsWith("***") && pendingQuote != null)
            {
                var attribution = CleanWikimarkup(line[2..].Trim());
                var year = ExtractYearFromAttribution(attribution);

                string quoteAuthor;
                string quoteSource;

                if (isPersonPage)
                {
                    // Person page: attribution is the source ("Source Title, Year")
                    quoteAuthor = pendingCharacter ?? authorName;
                    quoteSource = ExtractSourceFromAttribution(attribution);
                }
                else if (pendingCharacter != null)
                {
                    // Work page with character dialogue: character is author, page title is source
                    quoteAuthor = pendingCharacter;
                    quoteSource = sourceTitle;
                }
                else
                {
                    // Topic page: attribution is "Author Name, Source/Year"
                    quoteAuthor = ExtractAuthorFromAttribution(attribution);
                    quoteSource = sourceTitle;
                }

                results.Add(new ParsedQuote(
                    Text: pendingQuote,
                    AuthorName: quoteAuthor,
                    SourceTitle: quoteSource,
                    SourceType: sourceType,
                    SourceYear: year,
                    Tags: BuildTags(pageTitle, currentSection, sourceType)
                ));
                pendingQuote = null;
                pendingCharacter = null;
                continue;
            }

            // Quote line (*): top-level bullet
            if (line.StartsWith("*") && !line.StartsWith("**"))
            {
                FlushPending(ref pendingQuote, ref pendingCharacter, authorName, sourceTitle, currentSubsection, pageTitle, sourceType, isPersonPage, results);

                var raw = CleanWikimarkup(line[1..].Trim());
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (!isPersonPage)
                {
                    var (character, quote) = SplitCharacterPrefix(raw);
                    pendingCharacter = character.Length > 0 ? character : null;
                    pendingQuote = quote.Length > 0 ? quote : raw;
                }
                else
                {
                    pendingQuote = raw;
                    pendingCharacter = null;
                }
                continue;
            }

            // Any non-list line flushes a pending quote that had no attribution
            if (!line.StartsWith("*") && !string.IsNullOrWhiteSpace(line))
                FlushPending(ref pendingQuote, ref pendingCharacter, authorName, sourceTitle, currentSubsection, pageTitle, sourceType, isPersonPage, results);
        }

        FlushPending(ref pendingQuote, ref pendingCharacter, authorName, sourceTitle, currentSubsection, pageTitle, sourceType, isPersonPage, results);
        return results;
    }

    private void FlushPending(
        ref string? pendingQuote, ref string? pendingCharacter,
        string authorName, string sourceTitle, string subsection,
        string pageTitle, SourceType sourceType, bool isPersonPage,
        List<ParsedQuote> results)
    {
        if (pendingQuote is null) return;

        // Ignore single-letter subsections (alphabetical organization on topic pages)
        var meaningfulSubsection = subsection.Length > 1 ? subsection : string.Empty;

        results.Add(new ParsedQuote(
            Text: pendingQuote,
            AuthorName: pendingCharacter ?? authorName,
            SourceTitle: isPersonPage ? string.Empty : (meaningfulSubsection.Length > 0 ? meaningfulSubsection : sourceTitle),
            SourceType: sourceType,
            SourceYear: null,
            Tags: BuildTags(pageTitle, meaningfulSubsection, sourceType)
        ));
        pendingQuote = null;
        pendingCharacter = null;
    }

    // ── Wikitext helpers ───────────────────────────────────────────────────────

    private static readonly Regex WikiLinkWithLabel = new(@"\[\[(?:[^\]|]+)\|([^\]]+)\]\]");
    private static readonly Regex WikiLink = new(@"\[\[([^\]]+)\]\]");
    private static readonly Regex ExternalLinkWithLabel = new(@"\[https?://\S+\s+([^\]]+)\]");
    private static readonly Regex ExternalLink = new(@"\[https?://\S+\]");
    private static readonly Regex WikiTemplate = new(@"\{\{[^}]*\}\}");
    private static readonly Regex HtmlRef = new(@"<ref[^>]*>.*?</ref>", RegexOptions.Singleline);
    private static readonly Regex HtmlTag = new(@"<[^>]+>");
    private static readonly Regex Bold = new(@"'''([^']+?)'''");
    private static readonly Regex Italic = new(@"''([^']+?)''");
    private static readonly Regex Footnote = new(@"\[\d+\]");
    private static readonly Regex MultiSpace = new(@"\s{2,}");

    private static string CleanWikimarkup(string text)
    {
        text = WikiLinkWithLabel.Replace(text, "$1");
        text = WikiLink.Replace(text, "$1");
        text = ExternalLinkWithLabel.Replace(text, "$1");
        text = ExternalLink.Replace(text, string.Empty);
        text = WikiTemplate.Replace(text, string.Empty);
        text = HtmlRef.Replace(text, string.Empty);
        text = HtmlTag.Replace(text, string.Empty);
        text = Bold.Replace(text, "$1");
        text = Italic.Replace(text, "$1");
        text = Footnote.Replace(text, string.Empty);
        text = MultiSpace.Replace(text.Trim(), " ");
        return text.Trim('"', '“', '”', ' ');
    }

    private static string StripSectionMarkers(string line, int markerLen) =>
        line[markerLen..^markerLen].Trim();

    private static (string character, string quote) SplitCharacterPrefix(string text)
    {
        var match = Regex.Match(text, @"^\[?([A-Z][^:\]]{1,50})\]?:\s*(.+)$", RegexOptions.Singleline);
        return match.Success
            ? (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim())
            : (string.Empty, text);
    }

    private static string ExtractSourceFromAttribution(string attribution)
    {
        var text = Regex.Replace(attribution, @"^[—–\-]+\s*", string.Empty).Trim();
        var match = Regex.Match(text, @"^([^,\(]+)");
        return match.Success ? match.Groups[1].Value.Trim() : text;
    }

    private static string ExtractAuthorFromAttribution(string attribution)
    {
        // "Bill Dudley, [1]" → "Bill Dudley"
        // "Vince Lombardi, as quoted in The New York Times" → "Vince Lombardi"
        var text = Regex.Replace(attribution, @"^[—–\-~]+\s*", string.Empty).Trim();
        text = Regex.Replace(text, @"\[\d+\]", string.Empty).Trim();
        var match = Regex.Match(text, @"^([^,\[]+)");
        return match.Success ? match.Groups[1].Value.Trim() : text;
    }

    private static int? ExtractYearFromAttribution(string attribution)
    {
        var match = Regex.Match(attribution, @"\b(1[5-9]\d{2}|20[0-2]\d)\b");
        return match.Success ? int.Parse(match.Value) : null;
    }

    private static bool IsPersonPage(List<string> categories) =>
        categories.Any(c => c.Contains("people", StringComparison.OrdinalIgnoreCase)
                         || c.Contains("born", StringComparison.OrdinalIgnoreCase)
                         || c.Contains("died", StringComparison.OrdinalIgnoreCase));

    private static SourceType DetectSourceType(string title, List<string> categories)
    {
        var cats = categories.Select(c => c.ToLowerInvariant()).ToList();
        if (cats.Any(c => c.Contains("film") || c.Contains("movie"))) return SourceType.Movie;
        if (cats.Any(c => c.Contains("television") || c.Contains("tv series") || c.Contains("animated"))) return SourceType.Television;
        if (cats.Any(c => c.Contains("novel") || c.Contains("book"))) return SourceType.Book;
        if (cats.Any(c => c.Contains("speech") || c.Contains("address"))) return SourceType.Speech;
        if (cats.Any(c => c.Contains("poem") || c.Contains("poetry"))) return SourceType.Poem;
        if (cats.Any(c => c.Contains("organization") || c.Contains("organisation") || c.Contains("company") || c.Contains("foundation") || c.Contains("institute"))) return SourceType.Organization;
        if (Regex.IsMatch(title, @"Season\s+\d|Episode|\(TV\)|\(TV series\)")) return SourceType.Television;
        return SourceType.Other;
    }

    private static string CleanTitle(string title) =>
        Regex.Replace(title, @"\s*\(.*?\)", string.Empty).Trim();

    private static bool IsMeta(string section)
    {
        var lower = section.ToLowerInvariant();
        return lower is "external links" or "see also" or "references" or "notes"
            or "misattributed" or "disputed" or "about" or "cast" or "crew"
            or "voice cast" or "main cast" or "recurring cast" or "guest cast"
            or "quotes about" or "film" or "television" or "music";
    }

    private static List<string> BuildTags(string pageTitle, string section, SourceType sourceType)
    {
        var tags = new List<string> { sourceType.ToString().ToLowerInvariant() };
        var slug = Regex.Replace(pageTitle.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (!string.IsNullOrWhiteSpace(slug)) tags.Add(slug);
        if (!string.IsNullOrWhiteSpace(section))
        {
            var sectionSlug = Regex.Replace(section.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (sectionSlug.Length is > 2 and < 40) tags.Add(sectionSlug);
        }
        return tags.Distinct().ToList();
    }

    // ── Wikiquote API ──────────────────────────────────────────────────────────

    private async Task<(string? wikitext, List<string> categories)> FetchPageAsync(string title, CancellationToken ct)
    {
        var url = $"{ApiBase}?action=query&titles={Uri.EscapeDataString(title)}"
                + "&prop=revisions|categories&rvprop=content&rvslots=main&cllimit=20&format=json";

        var response = await FetchAsync<WikiquotePageResponse>(url, ct);
        if (response is null) return (null, []);

        var page = response.Query.Pages.Values.FirstOrDefault();
        if (page is null || page.IsMissing) return (null, []);

        return (page.Wikitext, page.CategoryNames);
    }

    private async Task<T?> FetchAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            var json = await _http.GetStringAsync(url, ct);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikiquote API request failed: {Url}", url);
            return default;
        }
    }
}
