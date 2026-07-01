namespace IepAssistant.Api.DTOs.District;

/// <summary>
/// Query-string filters for the district audit-log viewer (Phase 2). All optional. Bound with
/// <c>[FromQuery]</c>. Date bounds arrive as UTC instants (the client converts local-day boundaries;
/// the upper bound is inclusive).
/// </summary>
public class AuditLogQueryRequest
{
    public int? StaffUserId { get; set; }
    public int? StudentId { get; set; }
    public string? Action { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    /// <summary>Keyset cursor: the last Id from the previous page (drives "Load more").</summary>
    public int? Cursor { get; set; }

    /// <summary>Page size. Default 25, max 100.</summary>
    public int? PageSize { get; set; }
}

/// <summary>A keyset page of audit entries plus the cursor for the next page (null when exhausted).</summary>
public class AuditLogPageDto
{
    public List<AuditLogEntryDto> Entries { get; set; } = new();
    public int? NextCursor { get; set; }
}

/// <summary>One enriched audit row. Display fields always render (fallbacks never leave a row blank).</summary>
public class AuditLogEntryDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public string ResourceDisplayName { get; set; } = string.Empty;
    public int? RecipientUserId { get; set; }
    public string? RecipientName { get; set; }
    public DateTime CreatedAt { get; set; }
}
