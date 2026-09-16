using System.Security.Cryptography;
using System.Text;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data;

/// <summary>
/// The single formula behind <see cref="AccessAuditLog.Hash"/> (pilot-gates plan, phase 1). Shared by
/// the writer (<c>AccessAuditLogWorker</c>, which computes it once a row's Id is assigned) and the
/// reader (the integrity-check service, which recomputes it to detect tampering) so the two can never
/// silently drift apart.
/// </summary>
public static class AuditHashChain
{
    /// <summary>
    /// SHA-256 over Id|Action|ActorUserId|ResourceType|ResourceId|RecipientUserId|CreatedAt|PrevHash,
    /// returned as 64 uppercase hex characters. <paramref name="createdAtUtc"/> is formatted round-trip
    /// ("O") so the input is unambiguous regardless of DB provider precision; <paramref name="prevHash"/>
    /// is the empty string for the very first row in the chain.
    /// </summary>
    /// <remarks>
    /// <paramref name="createdAtUtc"/>'s <see cref="DateTime.Kind"/> is forced to
    /// <see cref="DateTimeKind.Utc"/> before formatting, regardless of what it arrives as. Neither
    /// SQL Server's <c>datetime2</c> nor SQLite's TEXT storage preserve <see cref="DateTime.Kind"/>
    /// across a round trip — a freshly-created entity has <c>Kind=Utc</c> (from
    /// <see cref="DateTime.UtcNow"/>), but the SAME row read back from the database has
    /// <c>Kind=Unspecified</c>. "O" formatting includes a "Z"/offset suffix that depends on
    /// <see cref="DateTime.Kind"/>, so without this normalization the hash computed at insert time
    /// (writer, in-memory entity) would never match the hash recomputed at verification time
    /// (reader, freshly queried entity) for ANY row — a false "Broken" on every integrity run,
    /// despite zero actual tampering. The column is documented (and only ever populated) as UTC, so
    /// this is a safe, deliberate assumption, not a guess.
    /// </remarks>
    public static string ComputeHash(
        int id,
        AuditAction action,
        int actorUserId,
        string resourceType,
        int resourceId,
        int? recipientUserId,
        DateTime createdAtUtc,
        string? prevHash)
    {
        var input = string.Join(
            '|',
            id,
            action,
            actorUserId,
            resourceType,
            resourceId,
            recipientUserId?.ToString() ?? string.Empty,
            DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc).ToString("O"),
            prevHash ?? string.Empty);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }
}
