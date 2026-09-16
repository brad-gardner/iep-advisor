namespace IepAssistant.Api.Seeding;

/// <summary>
/// Backs `dotnet run --project IepAssistant.Api -- seed-demo [--reset]` (pilot-gates plan, phase 3).
/// Builds/removes the fictional "Maple Ridge Local Schools" (OH) demo district entirely through the
/// same real application services every other code path uses (roster, document/goal, meeting, family,
/// evaluation) so audit trail, goal-record projection and the PDF queue all happen exactly as they would
/// for a real district.
/// </summary>
public interface IDemoSeeder
{
    /// <summary>Idempotent: a no-op (Success, NoOp) when a demo district already exists.</summary>
    Task<DemoSeedResult> SeedAsync(CancellationToken ct = default);

    /// <summary>Deletes every row reachable from the demo district, including blobs. A no-op
    /// (Success, NoOp) when no demo district exists.</summary>
    Task<DemoSeedResult> ResetAsync(CancellationToken ct = default);
}

/// <summary>One row of the printed login summary. Password is the same fixed demo password for every
/// account, repeated per row for a copy-pasteable table.</summary>
public sealed record DemoLoginRow(string Role, string Email, string Password);

public sealed class DemoSeedResult
{
    public required bool Success { get; init; }
    public bool NoOp { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<DemoLoginRow> Logins { get; init; } = Array.Empty<DemoLoginRow>();

    public static DemoSeedResult Ok(string message, IReadOnlyList<DemoLoginRow> logins) =>
        new() { Success = true, Message = message, Logins = logins };

    public static DemoSeedResult NoOpResult(string message) =>
        new() { Success = true, NoOp = true, Message = message };

    public static DemoSeedResult Failure(string message) =>
        new() { Success = false, Message = message };
}
