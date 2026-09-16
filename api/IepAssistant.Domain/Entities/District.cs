namespace IepAssistant.Domain.Entities;

public class District : BaseEntity, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }

    /// <summary>Plan 6, decision 2: when false, staff may not share a draft with the family (the share
    /// action is hidden client-side and the endpoint refuses); finalized versions remain visible either way.</summary>
    public bool FamilyDraftSharingEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<School> Schools { get; set; } = new List<School>();
}
