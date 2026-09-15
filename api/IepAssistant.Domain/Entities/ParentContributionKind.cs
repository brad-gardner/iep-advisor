namespace IepAssistant.Domain.Entities;

/// <summary>What kind of "about my child" note a parent recorded (stored as string).</summary>
public enum ParentContributionKind
{
    Strength = 0,
    Concern = 1,
    WorksAtHome = 2,
    Priority = 3,
    Other = 4
}
