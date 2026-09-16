using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Offline family participation for a student (plan 7, decision 7): logged contact attempts and
/// input received outside the app, so a school-only student's family engagement is captured without a
/// family account. Feeds the meeting brief and the student evidence bundle.</summary>
public interface IFamilyContactService
{
    Task<ServiceResult<List<FamilyContactAttemptModel>>> GetContactAttemptsAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    Task<ServiceResult<FamilyContactAttemptModel>> RecordContactAttemptAsync(int userId, int schoolStudentId, CreateFamilyContactAttemptModel model, CancellationToken ct = default);

    Task<ServiceResult<List<OfflineFamilyInputModel>>> GetOfflineInputAsync(int userId, int schoolStudentId, CancellationToken ct = default);

    Task<ServiceResult<OfflineFamilyInputModel>> RecordOfflineInputAsync(int userId, int schoolStudentId, CreateOfflineFamilyInputModel model, CancellationToken ct = default);
}
