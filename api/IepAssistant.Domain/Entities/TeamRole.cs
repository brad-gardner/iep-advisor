namespace IepAssistant.Domain.Entities;

/// <summary>
/// Functional role of a staff member on a student's IEP team (plan 3, decision 4). Independent of the
/// permission tier (<see cref="AccessRole"/>) — the team role says what someone does, the access role says
/// what they may see/edit. Stored as a string.
/// </summary>
public enum TeamRole
{
    CaseManager,
    InterventionSpecialist,
    GeneralEducationTeacher,
    SpeechLanguagePathologist,
    OccupationalTherapist,
    PhysicalTherapist,
    SchoolPsychologist,
    Counselor,
    LeaRepresentative,
    Interpreter,
    Other
}

public static class TeamRoleExtensions
{
    public static string ToDisplay(this TeamRole role) => role switch
    {
        TeamRole.CaseManager => "Case Manager",
        TeamRole.InterventionSpecialist => "Intervention Specialist",
        TeamRole.GeneralEducationTeacher => "General Education Teacher",
        TeamRole.SpeechLanguagePathologist => "Speech-Language Pathologist",
        TeamRole.OccupationalTherapist => "Occupational Therapist",
        TeamRole.PhysicalTherapist => "Physical Therapist",
        TeamRole.SchoolPsychologist => "School Psychologist",
        TeamRole.Counselor => "Counselor",
        TeamRole.LeaRepresentative => "LEA Representative",
        TeamRole.Interpreter => "Interpreter",
        _ => "Team Member"
    };

    /// <summary>
    /// Default permission tier for a team role (plan 3, decision 4): the case manager owns the record,
    /// LEA representatives and interpreters read, everyone else collaborates. An admin may override.
    /// </summary>
    public static AccessRole DefaultAccessRole(this TeamRole role) => role switch
    {
        TeamRole.CaseManager => AccessRole.Owner,
        TeamRole.LeaRepresentative or TeamRole.Interpreter => AccessRole.Viewer,
        _ => AccessRole.Collaborator
    };
}
