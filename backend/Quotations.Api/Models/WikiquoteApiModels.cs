using System.Text.Json.Serialization;

namespace Quotations.Api.Models;

// ── All Pages ──────────────────────────────────────────────────────────────────

public class WikiquoteAllPagesResponse
{
    [JsonPropertyName("continue")]
    public AllPagesContinueToken? Continue { get; set; }

    [JsonPropertyName("query")]
    public AllPagesQuery Query { get; set; } = new();
}

public class AllPagesContinueToken
{
    [JsonPropertyName("apcontinue")]
    public string? ApContinue { get; set; }
}

public class AllPagesQuery
{
    [JsonPropertyName("allpages")]
    public List<WikiPageStub> AllPages { get; set; } = [];
}

public class WikiPageStub
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

// ── Recent Changes ─────────────────────────────────────────────────────────────

public class WikiquoteRecentChangesResponse
{
    [JsonPropertyName("continue")]
    public RecentChangesContinueToken? Continue { get; set; }

    [JsonPropertyName("query")]
    public RecentChangesQuery Query { get; set; } = new();
}

public class RecentChangesContinueToken
{
    [JsonPropertyName("rccontinue")]
    public string? RcContinue { get; set; }
}

public class RecentChangesQuery
{
    [JsonPropertyName("recentchanges")]
    public List<RecentChangeEntry> RecentChanges { get; set; } = [];
}

public class RecentChangeEntry
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

// ── Page Content (revisions + categories) ─────────────────────────────────────

public class WikiquotePageResponse
{
    [JsonPropertyName("query")]
    public PageContentQuery Query { get; set; } = new();
}

public class PageContentQuery
{
    [JsonPropertyName("pages")]
    public Dictionary<string, WikiquotePageContent> Pages { get; set; } = [];
}

public class WikiquotePageContent
{
    [JsonPropertyName("missing")]
    public string? Missing { get; set; }

    [JsonPropertyName("revisions")]
    public List<WikiRevision> Revisions { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<WikiCategory> Categories { get; set; } = [];

    public bool IsMissing => Missing != null;
    public string? Wikitext => Revisions.FirstOrDefault()?.Slots?.Main?.Content;
    public List<string> CategoryNames => Categories.Select(c => c.Title).ToList();
}

public class WikiRevision
{
    [JsonPropertyName("slots")]
    public WikiRevisionSlots? Slots { get; set; }
}

public class WikiRevisionSlots
{
    [JsonPropertyName("main")]
    public WikiRevisionMain? Main { get; set; }
}

public class WikiRevisionMain
{
    [JsonPropertyName("*")]
    public string? Content { get; set; }
}

public class WikiCategory
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}
