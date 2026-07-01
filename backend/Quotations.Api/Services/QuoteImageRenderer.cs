using SkiaSharp;

namespace Quotations.Api.Services;

/// <summary>
/// Renders a quotation to a branded, shareable PNG (also reusable as an OG image).
/// Stateless and thread-safe; registered as a singleton.
/// </summary>
public class QuoteImageRenderer
{
    private const int Width = 1200;
    private const int Height = 630; // standard OG image aspect ratio
    private const int Margin = 100;

    // Brand palette — mirrors the Lora-based cream/charcoal UI.
    private static readonly SKColor Background = new(0xFB, 0xF8, 0xF1);
    private static readonly SKColor QuoteColor = new(0x2B, 0x2B, 0x2B);
    private static readonly SKColor AuthorColor = new(0x6B, 0x4E, 0x2E);
    private static readonly SKColor AccentColor = new(0x16, 0xA3, 0x4A);
    private static readonly SKColor WordmarkColor = new(0x9A, 0x8C, 0x78);

    // Resolve a serif typeface once. Falls back to the default if no serif is installed.
    private static readonly SKTypeface SerifFace =
        SKFontManager.Default.MatchFamily("DejaVu Serif")
        ?? SKFontManager.Default.MatchFamily("Liberation Serif")
        ?? SKFontManager.Default.MatchCharacter('Q')
        ?? SKTypeface.Default;

    public byte[] Render(string text, string authorName, bool verified)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;
        canvas.Clear(Background);

        // Left accent rule
        using (var rule = new SKPaint { Color = AccentColor, IsAntialias = true })
            canvas.DrawRect(Margin - 28, Margin, 6, 70, rule);

        // Pick a quote font size that lets the text fit the box, then wrap.
        var (lines, fontSize) = LayoutQuote(text);

        using var quotePaint = new SKPaint { Color = QuoteColor, IsAntialias = true };
        using var quoteFont = new SKFont(SerifFace, fontSize);

        float lineHeight = fontSize * 1.32f;
        float blockHeight = lines.Count * lineHeight;
        // Vertically center the quote block in the upper ~75% of the canvas.
        float y = Margin + 60 + Math.Max(0, ((Height - 200 - blockHeight) / 2));

        foreach (var line in lines)
        {
            canvas.DrawText(line, Margin, y, SKTextAlign.Left, quoteFont, quotePaint);
            y += lineHeight;
        }

        // Author line
        using var authorPaint = new SKPaint { Color = AuthorColor, IsAntialias = true };
        using var authorFont = new SKFont(SerifFace, 34);
        canvas.DrawText($"— {authorName}", Margin, Height - Margin + 6, SKTextAlign.Left, authorFont, authorPaint);

        // Verified badge (bottom-right) when the attribution was AI-verified
        if (verified)
        {
            using var badgePaint = new SKPaint { Color = AccentColor, IsAntialias = true };
            using var badgeFont = new SKFont(SerifFace, 26);
            canvas.DrawText("✓ Verified", Width - Margin, Height - Margin + 4, SKTextAlign.Right, badgeFont, badgePaint);
        }

        // Wordmark (top-right)
        using var wordmarkPaint = new SKPaint { Color = WordmarkColor, IsAntialias = true };
        using var wordmarkFont = new SKFont(SerifFace, 26);
        canvas.DrawText("QuotationHub", Width - Margin, Margin + 4, SKTextAlign.Right, wordmarkFont, wordmarkPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Choose the largest font size (within a range) at which the wrapped quote
    /// fits the available height, and return the wrapped lines.
    /// </summary>
    private static (List<string> Lines, float FontSize) LayoutQuote(string text)
    {
        var quoted = $"“{text.Trim()}”";
        float maxWidth = Width - (2 * Margin);
        float maxHeight = Height - 230; // leave room for author + wordmark

        for (float size = 72; size >= 28; size -= 4)
        {
            using var font = new SKFont(SerifFace, size);
            var lines = WrapText(quoted, font, maxWidth);
            if (lines.Count * size * 1.32f <= maxHeight)
                return (lines, size);
        }

        using var smallest = new SKFont(SerifFace, 28);
        return (WrapText(quoted, smallest, maxWidth), 28);
    }

    private static List<string> WrapText(string text, SKFont font, float maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = "";

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureText(candidate) > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }
}
