using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class NotificationModel
{
    public int Id { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? LinkPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public string? EmailError { get; set; }
}

public class NotificationListModel
{
    public List<NotificationModel> Items { get; set; } = new();
    public int UnreadCount { get; set; }
}
