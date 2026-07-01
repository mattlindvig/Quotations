using FluentAssertions;
using Quotations.Api.Services;
using Xunit;

namespace Quotations.Tests.Unit;

public class QuoteImageRendererTests
{
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public void Render_ProducesValidPng()
    {
        var renderer = new QuoteImageRenderer();

        var bytes = renderer.Render(
            "The only thing we have to fear is fear itself.",
            "Franklin D. Roosevelt",
            verified: true);

        bytes.Should().NotBeNullOrEmpty();
        bytes.Take(PngMagic.Length).Should().Equal(PngMagic, "output should be a PNG");
    }

    [Fact]
    public void Render_HandlesLongTextAndMissingAuthor()
    {
        var renderer = new QuoteImageRenderer();
        var longText = string.Join(" ", Enumerable.Repeat("word", 120));

        var bytes = renderer.Render(longText, "", verified: false);

        bytes.Should().NotBeNullOrEmpty();
        bytes.Take(PngMagic.Length).Should().Equal(PngMagic);
    }
}
