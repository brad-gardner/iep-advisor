using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Resolves a student's team member <c>TeamRole</c> display name by user id — never the person's name —
/// for every surface that must show "Responsible: &lt;role&gt;" without identifying who that is (plan
/// 2026-10-02-002): the finalized PDF (<see cref="AuthoredDocumentPdfService"/>) and the family-facing
/// shared draft (<see cref="FamilyFacingValueRedactor"/> via <c>DraftSharingService</c>).
/// </summary>
public static class TeamRoleResolver
{
    /// <summary>
    /// One query: every row (active or not) for this student, collapsed to one role per user — the
    /// active membership wins when a user has both an active and a historical row, otherwise the most
    /// recently updated row. A former team member whose owner assignment is now stale on some row still
    /// resolves to a role rather than disappearing, since this is also used to label already-finalized
    /// historical content.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, string>> LoadRoleByUserIdAsync(
        ApplicationDbContext context, int schoolStudentId, CancellationToken ct)
    {
        var rows = await context.StudentTeamMembers
            .AsNoTracking()
            .Where(m => m.SchoolStudentId == schoolStudentId)
            .Select(m => new { m.UserId, m.TeamRole, m.IsActive, m.UpdatedAt })
            .ToListAsync(ct);

        return rows
            .GroupBy(m => m.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.IsActive).ThenByDescending(m => m.UpdatedAt).First().TeamRole.ToDisplay());
    }
}
