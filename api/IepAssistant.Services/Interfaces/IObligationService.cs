using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Computed procedural deadlines from <c>SchoolStudent</c> dates (plan 4, decision 2). Never persisted.</summary>
public interface IObligationService
{
    Task<ServiceResult<List<ObligationModel>>> GetForStudentAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>Non-admin: obligations for students where the caller is lead. Admin: their full scope.</summary>
    Task<ServiceResult<List<ObligationModel>>> GetMineAsync(int userId, ObligationStatus? status, CancellationToken ct = default);

    /// <summary>Admin-only: obligations across the caller's district (optionally narrowed to one school).</summary>
    Task<ServiceResult<List<ObligationModel>>> GetForScopeAsync(int userId, int? schoolId, ObligationStatus? status, CancellationToken ct = default);

    /// <summary>Obligations for students where <paramref name="userId"/> is personally the lead case
    /// manager — regardless of admin scope. Unlike <see cref="GetMineAsync"/>, a School/District admin gets
    /// no scope superset here: used by the anonymous per-user calendar feed token
    /// (<c>CalendarService.GetFeedByTokenAsync</c>), which must never carry an admin's whole scope over a
    /// non-expiring, unauthenticated link (review-fix contract addition 2, todos/066).</summary>
    Task<ServiceResult<List<ObligationModel>>> GetLeadOnlyAsync(int userId, CancellationToken ct = default);

    /// <summary>Batched form of <see cref="GetLeadOnlyAsync"/> for many staff at once: every DueSoon/Overdue-
    /// eligible obligation across all students led by any of <paramref name="userIds"/>, in one query,
    /// distinguished by <see cref="ObligationModel.OwnerUserId"/> (todos/067).</summary>
    Task<ServiceResult<List<ObligationModel>>> GetForLeadUsersAsync(IEnumerable<int> userIds, CancellationToken ct = default);

    /// <summary>Batched form of <see cref="GetMineAsync"/> for the digest: every staff user's obligations keyed by
    /// user id — lead-caseload for everyone, plus the full school/district scope for School/District admins —
    /// in a handful of queries regardless of staff count.</summary>
    Task<Dictionary<int, List<ObligationModel>>> GetForStaffDigestAsync(IEnumerable<int> userIds, CancellationToken ct = default);
}
