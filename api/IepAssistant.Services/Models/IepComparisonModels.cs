namespace IepAssistant.Services.Models;

public class TimelineEntry
{
    public int Id { get; set; }
    public DateTime? IepDate { get; set; }
    public string? MeetingType { get; set; }
    public string Status { get; set; } = string.Empty;
    public int GoalCount { get; set; }
    public int SectionCount { get; set; }
    public int RedFlagCount { get; set; }
    public bool HasAnalysis { get; set; }
}

public class TimelineResult
{
    public int ChildId { get; set; }
    public List<TimelineEntry> Ieps { get; set; } = [];
}

public class GoalChangeDetail
{
    public string Field { get; set; } = string.Empty;
    public string? Older { get; set; }
    public string? Newer { get; set; }
}

public class GoalDiff
{
    public string? Domain { get; set; }
    public string GoalText { get; set; } = string.Empty;
}

public class ModifiedGoalDiff
{
    public string? Domain { get; set; }
    public string OlderGoalText { get; set; } = string.Empty;
    public string NewerGoalText { get; set; } = string.Empty;
    public List<GoalChangeDetail> Changes { get; set; } = [];
}

public class GoalChanges
{
    public List<GoalDiff> Added { get; set; } = [];
    public List<GoalDiff> Removed { get; set; } = [];
    public List<ModifiedGoalDiff> Modified { get; set; } = [];
}

public class SectionChanges
{
    public List<string> Added { get; set; } = [];
    public List<string> Removed { get; set; } = [];
    public List<string> InBoth { get; set; } = [];
}

public class RedFlagChange
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RedFlagResolution
{
    public List<RedFlagChange> Resolved { get; set; } = [];
    public List<RedFlagChange> Persisting { get; set; } = [];
    public List<RedFlagChange> NewFlags { get; set; } = [];
}

public class ComparisonSummary
{
    public int GoalsAdded { get; set; }
    public int GoalsRemoved { get; set; }
    public int GoalsModified { get; set; }
    public int GoalsUnchanged { get; set; }
    public int SectionsAdded { get; set; }
    public int SectionsRemoved { get; set; }
    public int RedFlagsResolved { get; set; }
    public int RedFlagsPersisting { get; set; }
    public int NewRedFlags { get; set; }
}

public class ComparisonResult
{
    public int OlderIepId { get; set; }
    public int NewerIepId { get; set; }
    public DateTime? OlderDate { get; set; }
    public DateTime? NewerDate { get; set; }
    public GoalChanges GoalChanges { get; set; } = new();
    public SectionChanges SectionChanges { get; set; } = new();
    public RedFlagResolution RedFlagResolution { get; set; } = new();
    public ComparisonSummary Summary { get; set; } = new();
}
