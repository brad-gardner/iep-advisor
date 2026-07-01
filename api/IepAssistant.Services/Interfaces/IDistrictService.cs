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
}
