namespace IepAssistant.Domain.Entities;

public class District : BaseEntity, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<School> Schools { get; set; } = new List<School>();
}
