using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Shared write path for IEP team membership, used by <see cref="StudentTeamService"/>, the bulk
/// case-manager assignment in <see cref="EducatorService"/> and the roster importer. Keeps the two
/// invariants in one place: (1) a member always has a matching <c>SchoolStudentAccess</c> row, and (2) at
/// most one ACTIVE member is the lead — mirrored to <c>SchoolStudent.CaseManagerUserId</c>. Callers wrap
/// the call in a transaction: the lead swap is two saves (demote, then promote) so the filtered unique
/// index on the active lead is never tripped mid-batch. Multi-student callers use
/// <see cref="StudentTeamBatch"/>, which applies the same rules set-based.
/// </summary>
internal static class StudentTeamWriter
{
    /// <summary>
    /// Validates that <paramref name="target"/> may sit on a team for a student at <paramref name="studentSchoolId"/>:
    /// active, same district, not a DistrictAdmin, and either at that school or a RelatedServiceProvider.
    /// Returns a user-facing message on failure, null when allowed.
    /// </summary>
    public static string? ValidateTeamCandidate(StaffProfile? target, int districtId, int studentSchoolId)
    {
        if (target == null || !target.IsActive || target.DistrictId != districtId)
            return "Staff member not found.";
        if (target.OrgRoleId == OrgRoleIds.DistrictAdmin)
            return "A District Admin does not need a per-student assignment.";
        if (target.OrgRoleId == OrgRoleIds.RelatedServiceProvider)
            return null;
        if (target.SchoolId == null || target.SchoolId.Value != studentSchoolId)
            return "That staff member is not at this student's school.";
        return null;
    }

    /// <summary>
    /// Transfer rule: a staff member "follows" a student to <paramref name="newSchoolId"/> when they are
    /// based there, or serve every building (RelatedServiceProvider), or act by district scope
    /// (DistrictAdmin). Everyone else is deactivated from the team. Keep in sync with
    /// <see cref="LoadPortableUserIdsAsync"/>, its SQL-translated twin.
    /// </summary>
    public static bool IsPortable(int orgRoleId, int? schoolId, int newSchoolId)
        => schoolId == newSchoolId
           || orgRoleId == OrgRoleIds.RelatedServiceProvider
           || orgRoleId == OrgRoleIds.DistrictAdmin;

    /// <summary>User ids of the district's ACTIVE staff who may stay on a team after a move to <paramref name="newSchoolId"/>.</summary>
    public static async Task<HashSet<int>> LoadPortableUserIdsAsync(ApplicationDbContext context, int districtId, int newSchoolId, CancellationToken ct)
    {
        var ids = await context.StaffProfiles.AsNoTracking()
            .Where(p => p.IsActive && p.DistrictId == districtId
                     && (p.SchoolId == newSchoolId
                         || p.OrgRoleId == OrgRoleIds.RelatedServiceProvider
                         || p.OrgRoleId == OrgRoleIds.DistrictAdmin))
            .Select(p => p.UserId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>
    /// Creates or reactivates the (student, user) membership, upserts the access row, and optionally
    /// makes the member the lead. Access role: an explicit override always wins; a new or previously
    /// inactive access row gets the team-role default; an existing active row keeps its role.
    /// </summary>
    public static async Task<StudentTeamMember> UpsertMemberAsync(
        ApplicationDbContext context,
        SchoolStudent student,
        int targetUserId,
        TeamRole teamRole,
        bool makeLead,
        AccessRole? accessOverride,
        int actorUserId,
        CancellationToken ct)
    {
        var member = await context.StudentTeamMembers
            .FirstOrDefaultAsync(m => m.SchoolStudentId == student.Id && m.UserId == targetUserId, ct);
        var wasActive = member?.IsActive == true;
        if (member == null)
        {
            member = new StudentTeamMember
            {
                SchoolStudentId = student.Id,
                UserId = targetUserId,
                TeamRole = teamRole,
                IsLead = false,
                IsActive = true,
                CreatedById = actorUserId,
                UpdatedById = actorUserId
            };
            await context.StudentTeamMembers.AddAsync(member, ct);
        }
        else
        {
            member.IsActive = true;
            member.TeamRole = teamRole;
            member.Note = null;
            member.UpdatedById = actorUserId;
        }

        await UpsertAccessAsync(context, student.Id, targetUserId, teamRole, accessOverride, actorUserId, ct);
        await context.SaveChangesAsync(ct);

        // A CaseManager joining a student with no lead becomes the lead automatically.
        if (!makeLead && teamRole == TeamRole.CaseManager && !wasActive)
        {
            var hasLead = await context.StudentTeamMembers
                .AnyAsync(m => m.SchoolStudentId == student.Id && m.IsActive && m.IsLead && m.Id != member.Id, ct);
            makeLead = !hasLead;
        }

        if (makeLead && !member.IsLead)
            await PromoteToLeadAsync(context, student, member, actorUserId, ct);
        else if (member.IsLead)
            student.CaseManagerUserId = member.UserId;

        return member;
    }

    /// <summary>Makes <paramref name="member"/> the single active lead (demote-save, then promote-save).</summary>
    public static async Task PromoteToLeadAsync(ApplicationDbContext context, SchoolStudent student, StudentTeamMember member, int actorUserId, CancellationToken ct)
    {
        var currentLeads = await context.StudentTeamMembers
            .Where(m => m.SchoolStudentId == student.Id && m.IsLead && m.Id != member.Id)
            .ToListAsync(ct);
        foreach (var lead in currentLeads)
        {
            lead.IsLead = false;
            lead.UpdatedById = actorUserId;
        }
        if (currentLeads.Count > 0)
            await context.SaveChangesAsync(ct);

        member.IsLead = true;
        member.IsActive = true;
        member.UpdatedById = actorUserId;
        student.CaseManagerUserId = member.UserId;
        student.UpdatedById = actorUserId;
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Deactivates the membership and its access row; clears the lead mirror when it was the lead.</summary>
    public static async Task DeactivateMemberAsync(ApplicationDbContext context, SchoolStudent student, StudentTeamMember member, int actorUserId, string? note, CancellationToken ct)
    {
        var wasLead = member.IsLead;
        member.IsActive = false;
        member.IsLead = false;
        member.Note = note;
        member.UpdatedById = actorUserId;

        var access = await context.SchoolStudentAccesses
            .FirstOrDefaultAsync(a => a.SchoolStudentId == student.Id && a.UserId == member.UserId, ct);
        if (access != null && access.IsActive)
        {
            access.IsActive = false;
            access.UpdatedById = actorUserId;
        }

        if (wasLead && student.CaseManagerUserId == member.UserId)
            student.CaseManagerUserId = null;
        student.UpdatedById = actorUserId;
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Demotes the current lead without removing them (used by import <c>CLEAR</c>).</summary>
    public static async Task ClearLeadAsync(ApplicationDbContext context, SchoolStudent student, int actorUserId, CancellationToken ct)
    {
        var leads = await context.StudentTeamMembers
            .Where(m => m.SchoolStudentId == student.Id && m.IsLead)
            .ToListAsync(ct);
        foreach (var lead in leads)
        {
            lead.IsLead = false;
            lead.UpdatedById = actorUserId;
        }
        student.CaseManagerUserId = null;
        student.UpdatedById = actorUserId;
        await context.SaveChangesAsync(ct);
    }

    public static async Task UpsertAccessAsync(ApplicationDbContext context, int studentId, int targetUserId, TeamRole teamRole, AccessRole? accessOverride, int actorUserId, CancellationToken ct)
    {
        var access = await context.SchoolStudentAccesses
            .FirstOrDefaultAsync(a => a.SchoolStudentId == studentId && a.UserId == targetUserId, ct);
        if (access == null)
        {
            await context.SchoolStudentAccesses.AddAsync(new SchoolStudentAccess
            {
                SchoolStudentId = studentId,
                UserId = targetUserId,
                Role = accessOverride ?? teamRole.DefaultAccessRole(),
                IsActive = true,
                CreatedById = actorUserId,
                UpdatedById = actorUserId
            }, ct);
            return;
        }

        if (accessOverride != null)
            access.Role = accessOverride.Value;
        else if (!access.IsActive)
            access.Role = teamRole.DefaultAccessRole();
        access.IsActive = true;
        access.UpdatedById = actorUserId;
    }
}
