using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IEducatorService
{
    /// <summary>
    /// Self-serve educator onboarding. Find-or-creates a District by (Name, StateCode),
    /// find-or-creates a School by (DistrictId, Name), creates a StaffProfile for the
    /// user→school (idempotent — returns the existing profile if already onboarded), stamped as
    /// Teacher with DistrictId from the school, and sets the user's Role to Educator. Returns the
    /// educator profile. (Slated for removal in a later phase once invite-only signup lands.)
    /// </summary>
    Task<ServiceResult<EducatorProfileModel>> OnboardAsync(int userId, OnboardEducatorModel model, CancellationToken ct = default);

    /// <summary>Returns the user's StaffProfile + org role/district/school, or a failure if no profile exists.</summary>
    Task<ServiceResult<EducatorProfileModel>> GetMeAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a SchoolStudent under the educator's school and grants the educator an Owner
    /// SchoolStudentAccess. Requires the user to have an active StaffProfile bound to a school.
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
