namespace IepAssistant.Api.DTOs.Admin;

public class AdminDashboardStats
{
    // Users
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int AdminUsers { get; set; }
    public int UsersWithSubscription { get; set; }
    public int UsersOnboarded { get; set; }

    // Children
    public int TotalChildren { get; set; }

    // IEP Documents
    public int TotalDocuments { get; set; }
    public int DocumentsParsed { get; set; }
    public int DocumentsCreated { get; set; }
    public int DocumentsError { get; set; }

    // Analyses
    public int TotalAnalyses { get; set; }
    public int AnalysesCompleted { get; set; }
    public int AnalysesError { get; set; }

    // Advocacy Goals
    public int TotalGoals { get; set; }

    // Meeting Prep
    public int TotalChecklists { get; set; }
    public int ChecklistsCompleted { get; set; }

    // Usage
    public int TotalAnalysisUsage { get; set; }
    public int TotalMeetingPrepUsage { get; set; }

    // Beta Codes
    public int TotalBetaCodes { get; set; }
    public int RedeemedBetaCodes { get; set; }

    // Sharing
    public int TotalSharedAccess { get; set; }

    // Recent Activity (last 7 days)
    public int NewUsersLast7Days { get; set; }
    public int NewDocumentsLast7Days { get; set; }
    public int AnalysesLast7Days { get; set; }

    // Breakdowns
    public Dictionary<string, int> DocumentsByMeetingType { get; set; } = new();
    public Dictionary<string, int> GoalsByCategory { get; set; } = new();
    public Dictionary<string, int> ChildrenByDisabilityCategory { get; set; } = new();
    public Dictionary<string, int> UsersBySubscriptionStatus { get; set; } = new();
}
