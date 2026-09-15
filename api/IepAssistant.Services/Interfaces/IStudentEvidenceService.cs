using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Role-filtered evidence bundle for one school student (staff view).</summary>
public interface IStudentEvidenceService
{
    Task<ServiceResult<StudentEvidenceBundle>> BuildForStaffAsync(int userId, int schoolStudentId, CancellationToken ct = default);
}
