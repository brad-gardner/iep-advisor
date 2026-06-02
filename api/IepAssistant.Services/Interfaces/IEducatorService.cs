using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IEducatorService
{
    /// <summary>
    /// Self-serve educator onboarding. Find-or-creates a District by (Name, StateCode),
    /// find-or-creates a School by (DistrictId, Name), creates a TeacherProfile for the
    /// user→school (idempotent — returns the existing profile if already onboarded), and
    /// sets the user's Role to Educator. Returns the educator profile.
    /// </summary>
    Task<ServiceResult<EducatorProfileModel>> OnboardAsync(int userId, OnboardEducatorModel model, CancellationToken ct = default);

    /// <summary>Returns the user's TeacherProfile + school/district, or a failure if not onboarded.</summary>
    Task<ServiceResult<EducatorProfileModel>> GetMeAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a SchoolStudent under the educator's school and grants the educator an Owner
    /// SchoolStudentAccess. Requires the user to have a TeacherProfile.
    /// </summary>
    Task<ServiceResult<SchoolStudentModel>> CreateStudentAsync(int userId, CreateSchoolStudentModel model, CancellationToken ct = default);

    /// <summary>Lists active SchoolStudents in the educator's school (SchoolId-bound).</summary>
    Task<ServiceResult<List<SchoolStudentModel>>> GetStudentsAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the student only if it belongs to the educator's school AND the educator has
    /// SchoolStudentAccess; otherwise a failure. Enforces cross-school rejection.
    /// </summary>
    Task<ServiceResult<SchoolStudentModel>> GetStudentAsync(int userId, int studentId, CancellationToken ct = default);
}
