namespace IepAssistant.Services.Models;

public class CalendarItemModel
{
    /// <summary>"Meeting" or "Obligation".</summary>
    public string Kind { get; set; } = string.Empty;

    public DateTime Date { get; set; }
    public MeetingModel? Meeting { get; set; }
    public ObligationModel? Obligation { get; set; }
}

/// <summary>The raw feed token + issue time. The controller composes the absolute subscription URL (it has
/// the current request's scheme/host; the service layer deliberately does not depend on HttpContext).</summary>
public class CalendarFeedModel
{
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
