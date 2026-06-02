namespace IepAssistant.Domain.Entities;

/// <summary>
/// P8a. The fixed set of self-advocacy contribution types a student can add to their workspace.
/// Stored string-converted (HasConversion&lt;string&gt;) per the EF convention.
/// </summary>
public enum StudentEntryKind
{
    Strength,
    Interest,
    AccommodationRequest,
    MeetingStatement,
    AiInterviewAnswer
}
