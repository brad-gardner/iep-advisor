using System.Security.Cryptography;
using System.Text;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Pilot-gates plan, phase 2: a non-reversible fingerprint for free text that must never be logged in
/// the clear — draft/document content, a parent's question, an IEP goal — but that occasionally needs a
/// stable, correlatable stand-in in a log line (e.g. "did this same input recur across N failures").
/// <see cref="Hash"/> never returns the input; it is one-way (SHA-256), so no log aggregator or on-call
/// engineer can recover the original text from it.
/// </summary>
public static class LogSafety
{
    /// <summary>First 12 hex characters of SHA-256(text) — enough to distinguish inputs in a log
    /// search without being a meaningfully searchable/guessable proxy for short, low-entropy text.
    /// Returns a fixed placeholder for null/empty input rather than hashing nothing.</summary>
    public static string Hash(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "(empty)";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hashBytes)[..12];
    }
}
