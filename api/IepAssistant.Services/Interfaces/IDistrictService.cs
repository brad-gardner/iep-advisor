using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// District/school management for staff. Authorization is resolved per-request from the caller's
/// active <see cref="StaffContext"/> via <see cref="IOrgAccessService"/> (never from JWT claims):
/// reads (overview, school list) are open to any active staff in the district; mutations
/// (create/edit/deactivate school) are DistrictAdmin-only and confined to the caller's own district.
/// </summary>
public interface IDistrictService
{
    /// <summary>Overview of the caller's district. Any active staff in the district may read.</summary>
    Task<ServiceResult<DistrictOverviewModel>> GetOverviewAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Oversight dashboard aggregate for the caller's district. DistrictAdmin sees the whole district;
    /// SchoolAdmin sees only their own school's slice; Teacher is denied. Inactive schools/students are
    /// excluded from every count and list; an empty district returns a valid all-zero payload.
    /// </summary>
    Task<ServiceResult<DistrictDashboardModel>> GetDashboardAsync(int userId, CancellationToken ct = default);

    /// <summary>Active schools in the caller's district. Any active staff may read (school pickers).</summary>
    Task<ServiceResult<List<DistrictSchoolModel>>> GetSchoolsAsync(int userId, CancellationToken ct = default);

    /// <summary>Creates a school in the caller's district. DistrictAdmin only.</summary>
    Task<ServiceResult<DistrictSchoolModel>> CreateSchoolAsync(int userId, CreateSchoolModel model, CancellationToken ct = default);

    /// <summary>Edits a school in the caller's district. DistrictAdmin only.</summary>
    Task<ServiceResult<DistrictSchoolModel>> UpdateSchoolAsync(int userId, int schoolId, UpdateSchoolModel model, CancellationToken ct = default);

    /// <summary>
    /// Soft-deactivates a school (IsActive=false). DistrictAdmin only. Blocked while the school has
    /// active students or active staff (explicit message). 404-style failure for schools outside the
    /// caller's district (no existence leak).
    /// </summary>
    Task<ServiceResult> DeactivateSchoolAsync(int userId, int schoolId, CancellationToken ct = default);

    /// <summary>
    /// Plan 5: overdue/due-soon/unknown-date counts by school. DistrictAdmin sees the whole district
    /// (optionally narrowed to <paramref name="schoolId"/>); SchoolAdmin is forced to their own school
    /// regardless of <paramref name="schoolId"/>; every other role is denied. <paramref name="from"/>/
    /// <paramref name="to"/> (default: today .. today+60 days) bound ONLY the DueInRange bucket — Due30/
    /// Due60 are always anchored on today, so a non-default range never desyncs their drilldowns
    /// (review-fix contract addition 1). A value outside ±10 years of today fails with a bad-request
    /// result instead of overflowing date arithmetic.
    /// </summary>
    Task<ServiceResult<ComplianceBoardModel>> GetComplianceBoardAsync(int userId, int? schoolId, DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>Plan 5: staff/draft activity in the last <paramref name="days"/> days, by school. Same scoping as <see cref="GetComplianceBoardAsync"/>.</summary>
    Task<ServiceResult<AdoptionModel>> GetAdoptionAsync(int userId, int? schoolId, int days, CancellationToken ct = default);

    /// <summary>Plan 5: family-link evidence tile, by school. Same scoping as <see cref="GetComplianceBoardAsync"/>.</summary>
    Task<ServiceResult<EngagementModel>> GetEngagementAsync(int userId, int? schoolId, CancellationToken ct = default);
}
