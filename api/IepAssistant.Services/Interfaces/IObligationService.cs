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
}
