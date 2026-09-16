namespace IepAssistant.Api.DTOs.Admin;

public class OutboundEmailDto
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OutboundEmailStatusDto
{
    public bool Configured { get; set; }
    public int Queued { get; set; }
    public int Failed { get; set; }
    public DateTime? LastSentAt { get; set; }
}
