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
    /// Lists ACTIVE SchoolStudents the caller may open (unpaged; kept for existing callers — see
    /// <see cref="SearchStudentsAsync"/> for filters/paging). Role-branched so list authz == detail authz:
    /// Teacher-tier = students with an active SchoolStudentAccess; SchoolAdmin = whole school;
    /// DistrictAdmin = all students across active schools in the district.
    /// </summary>
    Task<ServiceResult<List<SchoolStudentModel>>> GetStudentsAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Roster search with the same role-branched scope as <see cref="GetStudentsAsync"/>, plus name /
    /// external-id search, school, status (default Active; null = all) and grade filters, and paging.
    /// </summary>
    Task<ServiceResult<PagedResult<SchoolStudentModel>>> SearchStudentsAsync(int userId, StudentSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns the student only if the caller may act on it at Viewer level (admins by scope, teachers by
    /// an active SchoolStudentAccess) — same authorization as <see cref="GetStudentsAsync"/>.
    /// </summary>
    Task<ServiceResult<SchoolStudentModel>> GetStudentAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>Full-replacement edit. Collaborator+ on the student, or an admin in scope.</summary>
    Task<ServiceResult<SchoolStudentModel>> UpdateStudentAsync(int userId, int studentId, UpdateSchoolStudentModel model, CancellationToken ct = default);

    /// <summary>Marks the student Exited (IsActive=false). Admin in scope only.</summary>
    Task<ServiceResult<SchoolStudentModel>> ExitStudentAsync(int userId, int studentId, ExitStudentModel model, CancellationToken ct = default);

    /// <summary>Returns an Exited/Archived student to Active and clears the exit fields. Admin in scope only.</summary>
    Task<ServiceResult<SchoolStudentModel>> ReactivateStudentAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>Marks the student Archived (IsActive=false). Admin in scope only.</summary>
    Task<ServiceResult<SchoolStudentModel>> ArchiveStudentAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>
    /// Moves the student to another active school of the district, keeping documents, links and team;
    /// team/access rows for staff not at the new school (and not RelatedServiceProvider/DistrictAdmin) are
    /// deactivated. DistrictAdmin only.
    /// </summary>
    Task<ServiceResult<SchoolStudentModel>> TransferStudentAsync(int userId, int studentId, int newSchoolId, CancellationToken ct = default);

    /// <summary>Sets/replaces the lead case manager on each student. Admin only; every student must be in scope.</summary>
    Task<ServiceResult<BulkAssignResultModel>> AssignCaseManagerBulkAsync(int userId, BulkAssignCaseManagerModel model, CancellationToken ct = default);

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
