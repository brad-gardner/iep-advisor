namespace IepAssistant.Domain.Entities;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
    int? CreatedById { get; set; }
    int? UpdatedById { get; set; }
}
