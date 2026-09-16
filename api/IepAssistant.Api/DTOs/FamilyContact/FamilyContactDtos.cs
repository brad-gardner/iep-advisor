using System.ComponentModel.DataAnnotations;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.FamilyContact;

public class FamilyContactAttemptDto
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

public class CreateFamilyContactAttemptRequest
{
    public DateTime? AttemptedAt { get; set; }
    [Required] public FamilyContactMethod? Method { get; set; }
    [Required] public FamilyContactOutcome? Outcome { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
}

public class OfflineFamilyInputDto
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

public class CreateOfflineFamilyInputRequest
{
    public int? DocumentInstanceId { get; set; }
    public DateTime? ReceivedAt { get; set; }
    [Required] public FamilyContactMethod? Method { get; set; }
    [Required, MaxLength(4000)] public string Summary { get; set; } = string.Empty;
}

internal static class FamilyContactMappers
{
    public static FamilyContactAttemptDto MapAttempt(FamilyContactAttemptModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        AttemptedAt = m.AttemptedAt,
        Method = m.Method,
        Outcome = m.Outcome,
        Note = m.Note,
        RecordedByUserId = m.RecordedByUserId,
        RecordedByName = m.RecordedByName
    };

    public static OfflineFamilyInputDto MapInput(OfflineFamilyInputModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        DocumentInstanceId = m.DocumentInstanceId,
        ReceivedAt = m.ReceivedAt,
        Method = m.Method,
        Summary = m.Summary,
        RecordedByUserId = m.RecordedByUserId,
        RecordedByName = m.RecordedByName
    };
}
