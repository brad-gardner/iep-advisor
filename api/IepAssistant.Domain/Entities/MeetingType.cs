namespace IepAssistant.Domain.Entities;

/// <summary>Kind of IEP-team meeting (plan 4, decision 1). Stored as a string.</summary>
public enum MeetingType
{
    AnnualReview,
    InitialIep,
    Amendment,
    EtrEligibility,
    Reevaluation,
    Transition,
    ManifestationDetermination,
    Other
}

public static class MeetingTypeExtensions
{
    public static string ToDisplay(this MeetingType type) => type switch
    {
        MeetingType.AnnualReview => "Annual Review",
        MeetingType.InitialIep => "Initial IEP",
        MeetingType.Amendment => "Amendment",
        MeetingType.EtrEligibility => "ETR/Eligibility",
        MeetingType.Reevaluation => "Reevaluation",
        MeetingType.Transition => "Transition",
        MeetingType.ManifestationDetermination => "Manifestation Determination",
        _ => "Meeting"
    };
}
