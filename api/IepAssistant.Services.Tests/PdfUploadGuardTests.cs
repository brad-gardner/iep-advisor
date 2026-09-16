using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

public class PdfUploadGuardTests
{
    [Theory]
    [InlineData("../../etc/evil.pdf", "evil.pdf")]
    [InlineData("a/../b.pdf", "b.pdf")]
    [InlineData("..pdf", "pdf")]
    [InlineData(".\0.pdf", "pdf")] // a NUL between two dots must not re-form ".." after the strip
    [InlineData("  ", "fallback.pdf")]
    [InlineData(null, "fallback.pdf")]
    [InlineData("signed copy.pdf", "signed copy.pdf")]
    public void SafeFileName_ReducesToABareNameAndIsIdempotent(string? input, string expected)
    {
        var once = PdfUploadGuard.SafeFileName(input, "fallback.pdf");
        Assert.Equal(expected, once);
        Assert.Equal(once, PdfUploadGuard.SafeFileName(once, "fallback.pdf"));
        Assert.DoesNotContain("..", once);
        Assert.DoesNotContain("/", once);
        Assert.DoesNotContain("\\", once);
    }

    [Fact]
    public void SafeFileName_BoundsTheLengthToTheColumn_KeepingTheExtension()
    {
        var name = PdfUploadGuard.SafeFileName(new string('a', 400) + ".pdf", "fallback.pdf");
        Assert.Equal(PdfUploadGuard.MaxFileNameLength, name.Length);
        Assert.EndsWith(".pdf", name);
    }

    [Fact]
    public void SafeFileName_TruncationNeverReformsTraversalOrSplitsASurrogatePair()
    {
        // A '.' landing at the cut would meet the ".pdf" seam and re-form "..".
        var seam = PdfUploadGuard.SafeFileName(new string('a', 255) + "." + new string('z', 5) + ".pdf", "fallback.pdf");
        Assert.DoesNotContain("..", seam);
        Assert.True(seam.Length <= PdfUploadGuard.MaxFileNameLength);
        Assert.EndsWith(".pdf", seam);

        // An emoji straddling the cut must not leave a lone surrogate behind.
        var emoji = PdfUploadGuard.SafeFileName("a" + string.Concat(Enumerable.Repeat("\U0001F600", 200)) + ".pdf", "fallback.pdf");
        Assert.True(emoji.Length <= PdfUploadGuard.MaxFileNameLength);
        for (var i = 0; i < emoji.Length; i++)
        {
            if (char.IsHighSurrogate(emoji[i]))
                Assert.True(i + 1 < emoji.Length && char.IsLowSurrogate(emoji[i + 1]), "lone high surrogate");
            else
                Assert.False(char.IsLowSurrogate(emoji[i]) && (i == 0 || !char.IsHighSurrogate(emoji[i - 1])), "lone low surrogate");
        }
        Assert.Equal(emoji, System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(emoji))); // UTF-8 round trip intact
    }

    [Fact]
    public async Task LooksLikePdf_ChecksTheMagicBytes_AndRewinds()
    {
        using var pdf = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 ..."));
        pdf.Position = 3;
        Assert.True(await PdfUploadGuard.LooksLikePdfAsync(pdf));
        Assert.Equal(0, pdf.Position);

        using var exe = new MemoryStream(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 });
        Assert.False(await PdfUploadGuard.LooksLikePdfAsync(exe));

        using var tiny = new MemoryStream(new byte[] { 0x25, 0x50 });
        Assert.False(await PdfUploadGuard.LooksLikePdfAsync(tiny));
    }
}
