namespace IepAssistant.Services.Models;

/// <summary>
/// Closed vocabularies for the optional <c>semantic</c> tag a template author can put on a field
/// (and on a Table column). Semantics let AI assist, prefill, PDF layout and downstream projections
/// find "the goals table" or "the baseline column" in ANY template — seeded or district-authored —
/// without hard-coding FieldKey GUIDs. Unknown values are rejected by
/// <see cref="Implementations.TemplateFieldConfigValidator"/>; a missing semantic is always valid.
/// </summary>
public static class FieldSemantics
{
    public const string StudentProfile = "studentProfile";
    public const string PresentLevels = "presentLevels";
    public const string Eligibility = "eligibility";
    public const string Placement = "placement";
    public const string ProgressMonitoring = "progressMonitoring";
    public const string SpecialFactors = "specialFactors";
    public const string Goals = "goals";
    public const string Services = "services";
    public const string Accommodations = "accommodations";
    public const string Transition = "transition";
    public const string FuturePlanning = "futurePlanning";
    public const string ExtendedSchoolYear = "extendedSchoolYear";
    public const string Testing = "testing";
    public const string Transportation = "transportation";
    public const string Lre = "lre";
    public const string Participants = "participants";
    public const string Signatures = "signatures";
    public const string ReferralReason = "referralReason";
    public const string EvaluationPlan = "evaluationPlan";
    public const string EvaluatorReports = "evaluatorReports";
    public const string TeamSummary = "teamSummary";
    public const string EligibilityDetermination = "eligibilityDetermination";
    public const string MeetingDate = "meetingDate";
    public const string EffectiveDates = "effectiveDates";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        StudentProfile, PresentLevels, Eligibility, Placement, ProgressMonitoring, SpecialFactors,
        Goals, Services, Accommodations, Transition, FuturePlanning, ExtendedSchoolYear, Testing,
        Transportation, Lre, Participants, Signatures, ReferralReason, EvaluationPlan, EvaluatorReports,
        TeamSummary, EligibilityDetermination, MeetingDate, EffectiveDates
    };

    /// <summary>Semantics that identify a repeating structured block (a Table whose rows have identity).</summary>
    public static readonly IReadOnlySet<string> RowBlocks = new HashSet<string>(StringComparer.Ordinal)
    {
        Goals, Services, Accommodations, Transition, Participants, EvaluatorReports
    };
}

/// <summary>Closed vocabulary for Table column semantics (see <see cref="FieldSemantics"/>).</summary>
public static class ColumnSemantics
{
    // Goals
    public const string Domain = "domain";
    public const string GoalText = "goalText";
    public const string Baseline = "baseline";
    public const string TargetCriteria = "targetCriteria";
    public const string MeasurementMethod = "measurementMethod";
    public const string Timeframe = "timeframe";
    // Services
    public const string ServiceType = "serviceType";
    public const string Frequency = "frequency";
    public const string Duration = "duration";
    public const string Location = "location";
    public const string ProviderRole = "providerRole";
    public const string StartDate = "startDate";
    public const string EndDate = "endDate";
    // Accommodations
    public const string Category = "category";
    public const string Accommodation = "accommodation";
    // Transition
    public const string GoalArea = "goalArea";
    public const string TransitionServices = "transitionServices";
    // Participants / evaluators
    public const string ParticipantName = "participantName";
    public const string ParticipantRole = "participantRole";
    public const string Attended = "attended";
    public const string EvaluationDomain = "evaluationDomain";
    public const string EvaluatorName = "evaluatorName";
    public const string Findings = "findings";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Domain, GoalText, Baseline, TargetCriteria, MeasurementMethod, Timeframe,
        ServiceType, Frequency, Duration, Location, ProviderRole, StartDate, EndDate,
        Category, Accommodation, GoalArea, TransitionServices,
        ParticipantName, ParticipantRole, Attended, EvaluationDomain, EvaluatorName, Findings
    };
}

/// <summary>
/// Reserved keys inside a Table row object that are NOT columns: row identity and provenance
/// metadata. They are preserved by value coercion, never rendered as cells, and never stripped as
/// "unknown columns".
/// </summary>
public static class RowMetaKeys
{
    /// <summary>Stable server-assigned row identity (GUID string). This is the lineage key for goals,
    /// services and accommodations across saves, finalizes and carried-forward documents.</summary>
    public const string RowId = "_rowId";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { RowId };
}
