namespace IepAssistant.Services.Implementations;

/// <summary>
/// The two checks every PDF upload in this codebase applies (see <c>IepDocumentsController</c>,
/// <c>EtrDocumentsController</c>, <c>ProgressReportsController</c>), shared so the plan-7 upload
/// paths (evaluation consent, signed artifacts) cannot drift from them: the bytes must start with
/// the <c>%PDF-</c> magic header (a declared Content-Type is not trusted), and the client's file
/// name is reduced to a bare name before it is stored or ever used to build a path.
/// </summary>
public static class PdfUploadGuard
{
    private const string Magic = "%PDF-";

    /// <summary>True when the stream's first bytes are the PDF magic header. Rewinds the stream.</summary>
    public static async Task<bool> LooksLikePdfAsync(Stream stream, CancellationToken ct = default)
    {
        if (!stream.CanSeek)
            return false; // multipart form files are always seekable; anything else is refused rather than guessed
        stream.Position = 0;
        var header = new byte[Magic.Length];
        var read = 0;
        while (read < header.Length)
        {
            var n = await stream.ReadAsync(header.AsMemory(read, header.Length - read), ct);
            if (n == 0) break;
            read += n;
        }
        stream.Position = 0;
        return read == header.Length && System.Text.Encoding.ASCII.GetString(header) == Magic;
    }

    /// <summary>
    /// A file name safe to persist and to embed in a storage or archive path: directory parts are
    /// stripped, path separators and traversal segments removed, and an empty result falls back.
    /// </summary>
    public static string SafeFileName(string? fileName, string fallback)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Trim());
        name = name.Replace("\\", "").Replace("/", "").Replace("..", "");
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "");
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
