using System.Security.Cryptography;
using System.Text;

namespace IepAssistant.Services.Security;

/// <summary>
/// Shared single-use invite-token helper (consolidated from the duplicated private methods in
/// <c>ChildLinkService</c> and <c>StudentInviteService</c>, now also used by <c>StaffInviteService</c>).
///
/// Contract — behaviour-identical to the originals it replaces:
/// <list type="bullet">
/// <item><see cref="Generate"/> returns a base64 string of 32 cryptographically-random bytes; this raw
/// token is emailed to the recipient and NEVER stored.</item>
/// <item><see cref="Hash"/> SHA-256s the UTF-8 bytes of a token and returns the base64 digest; only this
/// hash is persisted, and accept hashes the inbound token to match.</item>
/// </list>
/// </summary>
public static class InviteTokenHelper
{
    /// <summary>Generates a fresh raw token (32 random bytes, base64). Emailed, never stored.</summary>
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 hashes a token's UTF-8 bytes and returns the base64 digest (what gets stored).</summary>
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
