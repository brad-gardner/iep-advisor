using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// The one rule for "which state's special-education rules apply to this child": the district of the
/// school record the child is linked to (active, accepted <c>ChildLink</c>), then that record's school, then
/// the owning parent's profile state, else unknown. Every rung is normalised to a two-letter USPS code
/// (<see cref="Normalize"/>), and a rung whose value is unrecognised falls through to the next.
/// </summary>
public static class ChildStateResolver
{
    public static async Task<string?> ResolveAsync(ApplicationDbContext context, int childId, CancellationToken ct = default)
    {
        var linked = await context.ChildLinks.AsNoTracking()
            .Where(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null)
            .OrderByDescending(l => l.AcceptedAt).ThenByDescending(l => l.Id)
            .Select(l => new { DistrictState = l.SchoolStudent.District.StateCode, SchoolState = l.SchoolStudent.School.StateCode })
            .FirstOrDefaultAsync(ct);

        var fromSchoolRecord = Normalize(linked?.DistrictState) ?? Normalize(linked?.SchoolState);
        if (fromSchoolRecord != null) return fromSchoolRecord;

        var ownerState = await context.ChildProfiles.AsNoTracking()
            .Where(c => c.Id == childId)
            .Select(c => c.User.State)
            .FirstOrDefaultAsync(ct);
        return Normalize(ownerState);
    }

    /// <summary>
    /// A two-letter USPS code for the 50 states and DC, or null. Accepts the code in any case ("oh") and the
    /// full name ("Ohio", "new york"); anything else — blank, "XX", "Ohio " with extra words, territories — is null.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 2)
        {
            var code = trimmed.ToUpperInvariant();
            return Codes.Contains(code) ? code : null;
        }
        return NamesToCodes.TryGetValue(trimmed, out var fromName) ? fromName : null;
    }

    private static readonly IReadOnlyDictionary<string, string> NamesToCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Alabama"] = "AL", ["Alaska"] = "AK", ["Arizona"] = "AZ", ["Arkansas"] = "AR", ["California"] = "CA",
        ["Colorado"] = "CO", ["Connecticut"] = "CT", ["Delaware"] = "DE", ["District of Columbia"] = "DC", ["Florida"] = "FL",
        ["Georgia"] = "GA", ["Hawaii"] = "HI", ["Idaho"] = "ID", ["Illinois"] = "IL", ["Indiana"] = "IN",
        ["Iowa"] = "IA", ["Kansas"] = "KS", ["Kentucky"] = "KY", ["Louisiana"] = "LA", ["Maine"] = "ME",
        ["Maryland"] = "MD", ["Massachusetts"] = "MA", ["Michigan"] = "MI", ["Minnesota"] = "MN", ["Mississippi"] = "MS",
        ["Missouri"] = "MO", ["Montana"] = "MT", ["Nebraska"] = "NE", ["Nevada"] = "NV", ["New Hampshire"] = "NH",
        ["New Jersey"] = "NJ", ["New Mexico"] = "NM", ["New York"] = "NY", ["North Carolina"] = "NC", ["North Dakota"] = "ND",
        ["Ohio"] = "OH", ["Oklahoma"] = "OK", ["Oregon"] = "OR", ["Pennsylvania"] = "PA", ["Rhode Island"] = "RI",
        ["South Carolina"] = "SC", ["South Dakota"] = "SD", ["Tennessee"] = "TN", ["Texas"] = "TX", ["Utah"] = "UT",
        ["Vermont"] = "VT", ["Virginia"] = "VA", ["Washington"] = "WA", ["West Virginia"] = "WV", ["Wisconsin"] = "WI",
        ["Wyoming"] = "WY"
    };

    private static readonly HashSet<string> Codes = new(NamesToCodes.Values, StringComparer.Ordinal);
}
