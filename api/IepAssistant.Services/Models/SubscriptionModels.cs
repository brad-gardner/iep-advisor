namespace IepAssistant.Services.Models;

public class SubscriptionStatusModel
{
    public string Status { get; set; } = "none";
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<int, ChildUsageModel> ChildUsage { get; set; } = new();
}

public class ChildUsageModel
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public int AnalysisCount { get; set; }
    public int AnalysisLimit { get; set; } = 5;
}

public class BetaCodeModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsRedeemed { get; set; }
    public string? RedeemedByEmail { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
