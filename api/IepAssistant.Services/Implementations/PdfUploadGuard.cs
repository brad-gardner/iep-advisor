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

    /// <summary>The persisted column width for uploaded file names (SignedArtifact.FileName, EvaluationCase.ConsentFileName).</summary>
    public const int MaxFileNameLength = 260;

    /// <summary>
    /// A file name safe to persist and to embed in a storage or archive path: directory parts are
    /// stripped, path separators and traversal segments removed, the result bounded to
    /// <see cref="MaxFileNameLength"/> (keeping the extension), and an empty result falls back.
    /// </summary>
    public static string SafeFileName(string? fileName, string fallback)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Trim());
        // Strip until stable: removing an invalid character can re-form a ".." or a separator
        // (e.g. "." + NUL + "."), so a single pass is not enough for the result to be a bare name.
        string previous;
        do
        {
            previous = name;
            name = name.Replace("\\", "").Replace("/", "").Replace("..", "");
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
        } while (name != previous);
        if (string.IsNullOrWhiteSpace(name))
            return fallback;
        if (name.Length > MaxFileNameLength)
        {
            // A multipart file name has no filesystem behind it, so it can be arbitrarily long —
            // keep the extension (it is what viewers key on) and trim the stem.
            var ext = Path.GetExtension(name);
            if (ext.Length > 16) ext = string.Empty;
            name = name[..(MaxFileNameLength - ext.Length)] + ext;
        }
        return name;
    }
}
