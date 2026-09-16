using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Shared parent-authorization resolution: is the caller a parent linked to this SchoolStudent, and via
/// which of their own <c>ChildProfile</c>s? An active, accepted <see cref="ChildLink"/> to a
/// <see cref="ChildProfile"/> the caller holds at least <paramref name="minRole"/> access to (mirrors
/// <c>AuthoredDocumentVersionService.ParentCanViewStudentAsync</c>, extended to also return the granting
/// childId so callers can attribute usage/notifications/links to the right child).
/// </summary>
public static class ParentAccessResolver
{
    public static async Task<int?> ResolveChildIdAsync(
        ApplicationDbContext context, IAccessService accessService, int parentUserId, int schoolStudentId, AccessRole minRole, CancellationToken ct)
    {
        var linkedChildIds = await context.ChildLinks.AsNoTracking()
            .Where(l => l.SchoolStudentId == schoolStudentId && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null)
            .Select(l => l.ChildProfileId!.Value)
            .ToListAsync(ct);

        foreach (var childId in linkedChildIds)
        {
            if (await accessService.HasMinimumRoleAsync(childId, parentUserId, minRole, ct))
                return childId;
        }
        return null;
    }
}
