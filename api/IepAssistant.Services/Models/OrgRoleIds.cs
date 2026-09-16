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

    /// <summary>Same authz tier as Teacher, but may hold student access in ANY active school of the
    /// district (SLPs, OTs, psychologists who serve several buildings). Plan 3, decision 5.</summary>
    public const int RelatedServiceProvider = 4;

    /// <summary>Same authz tier and school binding as Teacher. Plan 3, decision 5.</summary>
    public const int GeneralEducator = 5;

    public static readonly IReadOnlyList<int> All = new[] { DistrictAdmin, SchoolAdmin, Teacher, RelatedServiceProvider, GeneralEducator };

    /// <summary>DistrictAdmin or SchoolAdmin — the player-coach tier that acts by scope, not by grant.</summary>
    public static bool IsAdmin(int orgRoleId) => orgRoleId is DistrictAdmin or SchoolAdmin;

    /// <summary>Teacher-tier roles that require an explicit per-student access row.</summary>
    public static bool IsStaffTier(int orgRoleId) => orgRoleId is Teacher or RelatedServiceProvider or GeneralEducator;

    /// <summary>True for a known seeded role id.</summary>
    public static bool IsKnown(int orgRoleId) => orgRoleId is >= DistrictAdmin and <= GeneralEducator;

    public static string? NameOf(int orgRoleId) => orgRoleId switch
    {
        DistrictAdmin => "DistrictAdmin",
        SchoolAdmin => "SchoolAdmin",
        Teacher => "Teacher",
        RelatedServiceProvider => "RelatedServiceProvider",
        GeneralEducator => "GeneralEducator",
        _ => null
    };

    public static int? FromName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "districtadmin" or "district admin" => DistrictAdmin,
        "schooladmin" or "school admin" => SchoolAdmin,
        "teacher" => Teacher,
        "relatedserviceprovider" or "related service provider" => RelatedServiceProvider,
        "generaleducator" or "general educator" => GeneralEducator,
        _ => null
    };
}
