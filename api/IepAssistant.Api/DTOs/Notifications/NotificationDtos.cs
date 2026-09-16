using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.Notifications;

public class NotificationDto
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

public class NotificationListDto
{
    public List<NotificationDto> Items { get; set; } = new();
    public int UnreadCount { get; set; }
}

public class MarkAllReadResponseDto
{
    public int Marked { get; set; }
}
