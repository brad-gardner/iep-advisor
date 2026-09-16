namespace IepAssistant.Domain.Entities;

/// <summary>An evaluator's domain assignment within an <see cref="EvaluationCase"/> (plan 7, decision 1).</summary>
public class EvaluatorAssignment : BaseEntity, IAuditableEntity
{
    public int EvaluationCaseId { get; set; }
    public int UserId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public EvaluationCase EvaluationCase { get; set; } = null!;
    public User User { get; set; } = null!;
}
