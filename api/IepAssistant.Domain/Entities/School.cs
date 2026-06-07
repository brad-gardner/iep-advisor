namespace IepAssistant.Domain.Entities;

public class School : BaseEntity, IAuditableEntity
{
    public int DistrictId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }

    /// <summary>Soft-delete flag. Deactivated (<c>false</c>) schools are hidden from directory/picker
    /// listings; deactivation is blocked while the school has active students or active staff.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public District District { get; set; } = null!;
    public ICollection<SchoolStudent> Students { get; set; } = new List<SchoolStudent>();
}
