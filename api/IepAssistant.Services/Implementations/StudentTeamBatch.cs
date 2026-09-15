using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Set-based counterpart of <see cref="StudentTeamWriter"/> for callers that touch many students in one
/// unit of work (roster import commit, bulk case-manager assignment, transfer). All team and access rows
/// for the affected students are loaded TRACKED up front (two queries), every mutation is applied in
/// memory, and <see cref="SaveAsync"/> flushes in at most two <c>SaveChangesAsync</c> calls: demotions
/// and everything else first, then the promotions of students that still had a different active lead —
/// so the filtered unique index on the active lead is never tripped mid-batch regardless of statement
/// order. Callers own the transaction. New (not yet inserted) students are supported: their rows are
/// attached through the <c>SchoolStudent</c> navigation so EF orders the inserts.
/// </summary>
internal sealed class StudentTeamBatch
{
    private readonly ApplicationDbContext _context;
    private readonly int _actorUserId;
    private readonly Dictionary<SchoolStudent, List<StudentTeamMember>> _members = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SchoolStudent, List<SchoolStudentAccess>> _accesses = new(ReferenceEqualityComparer.Instance);
    private readonly List<(SchoolStudent Student, StudentTeamMember Member)> _deferredPromotions = new();

    private StudentTeamBatch(ApplicationDbContext context, int actorUserId)
    {
        _context = context;
        _actorUserId = actorUserId;
    }

    /// <summary>Loads (tracked) every team and access row of the given already-persisted students.</summary>
    public static async Task<StudentTeamBatch> LoadAsync(ApplicationDbContext context, IReadOnlyCollection<SchoolStudent> students, int actorUserId, CancellationToken ct)
    {
        var batch = new StudentTeamBatch(context, actorUserId);
        var byId = new Dictionary<int, SchoolStudent>();
        foreach (var student in students)
        {
            if (student.Id != 0)
                byId[student.Id] = student;
        }

        foreach (var chunk in byId.Keys.Chunk(500))
        {
            var members = await context.StudentTeamMembers
                .Where(m => chunk.Contains(m.SchoolStudentId))
                .ToListAsync(ct);
            foreach (var member in members)
                batch.MembersOf(byId[member.SchoolStudentId]).Add(member);

            var accesses = await context.SchoolStudentAccesses
                .Where(a => chunk.Contains(a.SchoolStudentId))
                .ToListAsync(ct);
            foreach (var access in accesses)
                batch.AccessesOf(byId[access.SchoolStudentId]).Add(access);
        }
        return batch;
    }

    /// <summary>
    /// Creates or reactivates the (student, user) membership with its access row and makes that member
    /// the single active lead (mirrored to <c>CaseManagerUserId</c>). Returns the member.
    /// </summary>
    public StudentTeamMember AssignLead(SchoolStudent student, int targetUserId, TeamRole teamRole, AccessRole? accessOverride)
    {
        var members = MembersOf(student);
        var member = members.FirstOrDefault(m => m.UserId == targetUserId);
        if (member == null)
        {
            member = new StudentTeamMember
            {
                SchoolStudent = student,
                SchoolStudentId = student.Id,
                UserId = targetUserId,
                TeamRole = teamRole,
                IsLead = false,
                IsActive = true,
                CreatedById = _actorUserId,
                UpdatedById = _actorUserId
            };
            _context.StudentTeamMembers.Add(member);
            members.Add(member);
        }
        else
        {
            member.IsActive = true;
            member.TeamRole = teamRole;
            member.Note = null;
            member.UpdatedById = _actorUserId;
        }
        UpsertAccess(student, targetUserId, teamRole, accessOverride);

        var demotedActiveLead = false;
        foreach (var other in members.Where(m => m.IsLead && !ReferenceEquals(m, member)))
        {
            demotedActiveLead |= other.IsActive;
            other.IsLead = false;
            other.UpdatedById = _actorUserId;
        }

        student.CaseManagerUserId = targetUserId;
        student.UpdatedById = _actorUserId;
        if (member.IsLead)
            return member;
        if (demotedActiveLead)
            _deferredPromotions.Add((student, member)); // promoted in the second save, after the demotion lands
        else
            member.IsLead = true;
        return member;
    }

    /// <summary>Demotes the current lead without removing them (import <c>CLEAR</c>).</summary>
    public void ClearLead(SchoolStudent student)
    {
        foreach (var lead in MembersOf(student).Where(m => m.IsLead))
        {
            lead.IsLead = false;
            lead.UpdatedById = _actorUserId;
        }
        student.CaseManagerUserId = null;
        student.UpdatedById = _actorUserId;
    }

    /// <summary>
    /// Transfer side effect: deactivates the team and access rows of everyone who cannot follow the
    /// student to another building (see <see cref="StudentTeamWriter.IsPortable"/>) and clears the lead
    /// mirror when the lead is among them. Returns (members, accesses) deactivated.
    /// </summary>
    public (int Members, int Accesses) DeactivateNonPortable(SchoolStudent student, IReadOnlySet<int> portableUserIds, string note)
    {
        var members = 0;
        foreach (var member in MembersOf(student).Where(m => m.IsActive && !portableUserIds.Contains(m.UserId)))
        {
            if (member.IsLead && student.CaseManagerUserId == member.UserId)
                student.CaseManagerUserId = null;
            member.IsActive = false;
            member.IsLead = false;
            member.Note = note;
            member.UpdatedById = _actorUserId;
            members++;
        }
        var accesses = 0;
        foreach (var access in AccessesOf(student).Where(a => a.IsActive && !portableUserIds.Contains(a.UserId)))
        {
            access.IsActive = false;
            access.UpdatedById = _actorUserId;
            accesses++;
        }
        if (members > 0 || accesses > 0)
            student.UpdatedById = _actorUserId;
        return (members, accesses);
    }

    /// <summary>Flushes the batch: one save, plus a second one only when a lead swap was deferred.</summary>
    public async Task SaveAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
        if (_deferredPromotions.Count == 0)
            return;
        foreach (var (student, member) in _deferredPromotions)
        {
            member.IsLead = true;
            member.IsActive = true;
            member.UpdatedById = _actorUserId;
            student.CaseManagerUserId = member.UserId;
        }
        _deferredPromotions.Clear();
        await _context.SaveChangesAsync(ct);
    }

    private void UpsertAccess(SchoolStudent student, int targetUserId, TeamRole teamRole, AccessRole? accessOverride)
    {
        var accesses = AccessesOf(student);
        var access = accesses.FirstOrDefault(a => a.UserId == targetUserId);
        if (access == null)
        {
            access = new SchoolStudentAccess
            {
                SchoolStudent = student,
                SchoolStudentId = student.Id,
                UserId = targetUserId,
                Role = accessOverride ?? teamRole.DefaultAccessRole(),
                IsActive = true,
                CreatedById = _actorUserId,
                UpdatedById = _actorUserId
            };
            _context.SchoolStudentAccesses.Add(access);
            accesses.Add(access);
            return;
        }
        if (accessOverride != null)
            access.Role = accessOverride.Value;
        else if (!access.IsActive)
            access.Role = teamRole.DefaultAccessRole();
        access.IsActive = true;
        access.UpdatedById = _actorUserId;
    }

    private List<StudentTeamMember> MembersOf(SchoolStudent student)
    {
        if (!_members.TryGetValue(student, out var list))
            _members[student] = list = new List<StudentTeamMember>();
        return list;
    }

    private List<SchoolStudentAccess> AccessesOf(SchoolStudent student)
    {
        if (!_accesses.TryGetValue(student, out var list))
            _accesses[student] = list = new List<SchoolStudentAccess>();
        return list;
    }
}
