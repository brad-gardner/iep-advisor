using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IEducatorService
{
    /// <summary>Returns the user's StaffProfile + org role/district/school, or a failure if no profile exists.</summary>
    Task<ServiceResult<EducatorProfileModel>> GetMeAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a SchoolStudent and grants the creator an Owner SchoolStudentAccess. The target school is
    /// resolved by org role: DistrictAdmin must supply an explicit <c>SchoolId</c> (active, in their
    /// district); SchoolAdmin/Teacher default to their own school (an explicit mismatched school is denied).
    /// </summary>
    Task<ServiceResult<SchoolStudentModel>> CreateStudentAsync(int userId, CreateSchoolStudentModel model, CancellationToken ct = default);

    /// <summary>
    /// Lists active SchoolStudents the caller may open, role-branched so list authz == detail authz:
    /// Teacher = students with an active SchoolStudentAccess; SchoolAdmin = whole school;
    /// DistrictAdmin = all active students across active schools in the district.
    /// </summary>
    Task<ServiceResult<List<SchoolStudentModel>>> GetStudentsAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the student only if the caller may act on it at Viewer level (admins by scope, teachers by
    /// an active SchoolStudentAccess) — same authorization as <see cref="GetStudentsAsync"/>.
    /// </summary>
    Task<ServiceResult<SchoolStudentModel>> GetStudentAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>Lists active staff↔student access grants for a student. Caller needs Viewer access.</summary>
    Task<ServiceResult<List<StudentStaffAccessModel>>> GetStudentStaffAccessAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>
    /// Grants (or reactivates/updates the role of) a staff member's access to a student. ADMIN-only and
    /// scope-checked; the target staff must be active and bound to the student's school.
    /// </summary>
    Task<ServiceResult<StudentStaffAccessModel>> GrantStudentStaffAccessAsync(int userId, int studentId, GrantStudentStaffAccessModel model, CancellationToken ct = default);

    /// <summary>Deactivates a staff↔student access grant (IsActive=false). ADMIN-only and scope-checked.</summary>
    Task<ServiceResult> RevokeStudentStaffAccessAsync(int userId, int studentId, int accessId, CancellationToken ct = default);
}
