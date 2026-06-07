namespace IepAssistant.Services.Models;

/// <summary>
/// Stable seeded IDs for the <c>OrgRoles</c> lookup table (user decision: DB lookup table over a code
/// enum). These constants mirror the values seeded via EF <c>HasData</c> in
/// <c>OrgRoleConfiguration</c> and MUST stay in sync with that seed.
/// </summary>
public static class OrgRoleIds
{
    public const int DistrictAdmin = 1;
    public const int SchoolAdmin = 2;
    public const int Teacher = 3;
}
