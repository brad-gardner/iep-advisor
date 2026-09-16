using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class FamilyContactAttemptModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public DateTime AttemptedAt { get; set; }
    public FamilyContactMethod Method { get; set; }
    public FamilyContactOutcome Outcome { get; set; }
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
}

public class CreateFamilyContactAttemptModel
{
    public DateTime? AttemptedAt { get; set; }
    public FamilyContactMethod Method { get; set; }
    public FamilyContactOutcome Outcome { get; set; }
    public string? Note { get; set; }
}

public class OfflineFamilyInputModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int? DocumentInstanceId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public FamilyContactMethod Method { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
}

public class CreateOfflineFamilyInputModel
{
    public int? DocumentInstanceId { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public FamilyContactMethod Method { get; set; }
    public string Summary { get; set; } = string.Empty;
}
