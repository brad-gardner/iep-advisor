namespace IepAssistant.Domain.Entities;

public class ChildProfile : BaseEntity, IAuditableEntity
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? GradeLevel { get; set; }
    public string? DisabilityCategory { get; set; }
    public string? SchoolDistrict { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CurrentIepDocumentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public User User { get; set; } = null!;
    public IepDocument? CurrentIepDocument { get; set; }
}
