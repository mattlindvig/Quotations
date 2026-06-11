using System.Net;
using System.Text.RegularExpressions;

namespace Quotations.Api.Services;

public static class TextNormalizer
{
    // Ensure a space follows . , ! ? ; : when the next char is a letter or digit
    private static readonly Regex MissingSpace =
        new(@"([.,!?;:])([^\s""'\)\]\}])", RegexOptions.Compiled);

    // Collapse runs of horizontal whitespace (tabs, spaces) to a single space
    private static readonly Regex MultipleSpaces =
        new(@"[ \t]{2,}", RegexOptions.Compiled);

    // Straight double-quote wrappers: "…"
    private static readonly Regex WrappedStraightQuotes =
        new(@"^""(.+)""$", RegexOptions.Singleline | RegexOptions.Compiled);

    // Curly double-quote wrappers: "…"
    private static readonly Regex WrappedCurlyQuotes =
        new(@"^“(.+)”$", RegexOptions.Singleline | RegexOptions.Compiled);

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Decode HTML entities (&nbsp; &amp; &mdash; &ldquo; etc.) before any other processing
        text = WebUtility.HtmlDecode(text);

        // Non-breaking spaces (U+00A0) — common artifact of &nbsp; — become regular spaces
        // so MultipleSpaces below can collapse them
        text = text.Replace(' ', ' ');

        // Strip carriage returns
        text = text.Replace("\r", string.Empty);

        // Strip wrapping quotes added by the source (e.g. `"the quote text"`)
        text = WrappedStraightQuotes.Replace(text, "$1");
        text = WrappedCurlyQuotes.Replace(text, "$1");

        text = MissingSpace.Replace(text, "$1 $2");
        text = MultipleSpaces.Replace(text, " ");
        return text.Trim();
    }
}
