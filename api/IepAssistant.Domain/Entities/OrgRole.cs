namespace IepAssistant.Domain.Entities;

/// <summary>
/// Org-role lookup table (user decision: a DB lookup table, not a code enum). Seeded with stable IDs
/// 1=DistrictAdmin, 2=SchoolAdmin, 3=Teacher. Referenced by <see cref="StaffProfile.OrgRoleId"/>.
/// Code-side constants live in <c>IepAssistant.Services.Models.OrgRoleIds</c>.
/// </summary>
public class OrgRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<StaffProfile> StaffProfiles { get; set; } = new List<StaffProfile>();
}
