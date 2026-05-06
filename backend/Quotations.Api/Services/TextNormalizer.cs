using System.Text.RegularExpressions;

namespace Quotations.Api.Services;

public static class TextNormalizer
{
    // Ensure a space follows . , ! ? ; : when the next char is a letter or digit
    private static readonly Regex MissingSpace =
        new(@"([.,!?;:])([^\s""'\)\]\}])", RegexOptions.Compiled);

    // Collapse runs of whitespace (excluding newlines) to a single space
    private static readonly Regex MultipleSpaces =
        new(@"[^\S\n]{2,}", RegexOptions.Compiled);

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = MissingSpace.Replace(text, "$1 $2");
        text = MultipleSpaces.Replace(text, " ");
        return text.Trim();
    }
}
