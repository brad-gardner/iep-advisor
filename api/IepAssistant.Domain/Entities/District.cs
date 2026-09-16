namespace IepAssistant.Domain.Entities;

public class District : BaseEntity, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }

    /// <summary>Plan 6, decision 2: when false, staff may not share a draft with the family (the share
    /// action is hidden client-side and the endpoint refuses); finalized versions remain visible either way.</summary>
    public bool FamilyDraftSharingEnabled { get; set; } = true;

    /// <summary>Pilot-gates plan, phase 3: tags the fictional seed district ("Maple Ridge Local
    /// Schools") created/removed by <c>seed-demo</c>/<c>seed-demo --reset</c>. Never set on a real
    /// district; used to scope the reset's deletes.</summary>
    public bool IsDemo { get; set; }

    /// <summary>Pilot-gates plan, phase 3 (C11 adoption slice): when true, staff invited as
    /// RelatedServiceProvider/GeneralEducator in this district may sign in via a 15-minute emailed
    /// magic link instead of a password. Default true.</summary>
    public bool MagicLinkEnabled { get; set; } = true;

    /// <summary>When true (default), a magic-link consume for a user with no MFA enrolled does not grant
    /// a full session — see <c>MagicLinkService.ConsumeAsync</c>.</summary>
    public bool RequireMfaForMagicLink { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<School> Schools { get; set; } = new List<School>();
}
