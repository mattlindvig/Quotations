using System.Text.RegularExpressions;

namespace Quotations.Api.Services;

public static class QuotationGarbageFilter
{
    // Matches patterns like "Season 1; Season 2" or "Episode 3; Episode 4"
    private static readonly Regex SeasonEpisodeList =
        new(@"(Season|Episode)\s+\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches lines that look like navigation or menu text
    private static readonly Regex NavigationLike =
        new(@"^(Home|Menu|Navigation|Contents?|Index|Chapter \d+|Part \d+)\s*[|:–—]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsLikelyGarbage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var trimmed = text.Trim();

        // Too short to be a real quote
        var wordCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < 4) return true;

        // Too long — likely scraped page content, not a quote
        if (wordCount > 500) return true;

        // Semicolon-heavy list (4+ semicolons = probably a list of items, not a quote)
        var semicolonCount = 0;
        foreach (var c in trimmed) if (c == ';') semicolonCount++;
        if (semicolonCount >= 4) return true;

        // "Season 1; Season 2" or "Episode 3; Episode 4" patterns
        if (SeasonEpisodeList.Matches(trimmed).Count >= 2) return true;

        // Navigation/menu-like header text
        if (NavigationLike.IsMatch(trimmed)) return true;

        return false;
    }
}
