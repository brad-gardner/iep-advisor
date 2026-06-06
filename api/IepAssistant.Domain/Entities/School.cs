namespace IepAssistant.Domain.Entities;

public class School : BaseEntity, IAuditableEntity
{
    public int DistrictId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public District District { get; set; } = null!;
    public ICollection<SchoolStudent> Students { get; set; } = new List<SchoolStudent>();
}
