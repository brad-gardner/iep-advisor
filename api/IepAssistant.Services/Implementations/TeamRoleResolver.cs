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
/// <remarks>
/// Review pass 2 decision: this intentionally keeps resolving INACTIVE members, rather than being
/// narrowed to active-only. <see cref="AuthoredDocumentVersionService.FinalizeAsync"/> (via
/// <see cref="OwnerEligibleRowSanitizer"/>) now strips an inactive owner's <c>_ownerUserId</c> from every
/// NEWLY finalized version, so a fresh finalize/PDF/GoalRecord simply has no row left to resolve a role
/// for. The inactive branch here still matters for (1) a version finalized before this fix shipped, whose
/// frozen ValuesJson may still carry a departed owner and should keep showing "Responsible: &lt;role&gt;"
/// rather than silently losing that historical detail, and (2) a shared DRAFT (not yet finalized), whose
/// live ValuesJson can briefly carry a stale owner between a save and the next edit/finalize. Narrowing
/// this resolver to active-only would not change what a new finalize freezes (already handled upstream)
/// but WOULD blank out legitimate historical/in-flight role labels — so it stays as a superset lookup.
/// </remarks>
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
