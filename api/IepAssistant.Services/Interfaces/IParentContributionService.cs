using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Parent-authored "about my child" contributions. Parents (Collaborator+ on the child) manage them;
/// linked school staff (Viewer+ on the school student) read only the ones marked shared.
/// </summary>
public interface IParentContributionService
{
    Task<ServiceResult<List<ParentContributionModel>>> GetForChildAsync(int childId, int userId, CancellationToken ct = default);
    Task<ServiceResult<ParentContributionModel>> CreateAsync(int childId, int userId, SaveParentContributionModel model, CancellationToken ct = default);
    Task<ServiceResult<ParentContributionModel>> UpdateAsync(int id, int userId, SaveParentContributionModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken ct = default);
    /// <summary>Shared contributions from every linked family of a school student (staff view).</summary>
    Task<ServiceResult<List<ParentContributionModel>>> GetSharedForSchoolStudentAsync(int educatorUserId, int schoolStudentId, CancellationToken ct = default);
}
