namespace IepAssistant.Services.Models;

/// <summary>
/// Filters for the district audit-log viewer (Phase 2). All fields are optional. This is the read-side
/// query shape; it never touches the write-only <c>AuditLogger</c> channel. Validation (invalid action,
/// non-positive page size, negative cursor) is performed by <c>AuditLogQueryService</c>.
/// </summary>
public class AuditLogQuery
{
    /// <summary>Restrict to a single actor (staff member) — intersected with the caller's actor scope.</summary>
    public int? StaffUserId { get; set; }

    /// <summary>
    /// Restrict to activity touching one student. Expanded to the set of (ResourceType, ResourceId) pairs:
    /// the student's own SchoolStudent row, its IepDraft rows, and the IepVersion rows under those drafts.
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>Case-insensitive <c>AuditAction</c> name (View/Edit/Share/Export/Finalize). Invalid ⇒ 400.</summary>
    public string? Action { get; set; }

    /// <summary>Inclusive lower bound on CreatedAt (UTC instant).</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Inclusive upper bound on CreatedAt (UTC instant).</summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>Keyset cursor: the last Id seen on the previous page. Rows with Id &lt; cursor are returned.</summary>
    public int? Cursor { get; set; }

    /// <summary>Page size. Default 25, max 100; ≤ 0 ⇒ 400.</summary>
    public int? PageSize { get; set; }
}

/// <summary>One enriched audit row for the viewer. Display fields always render (fallbacks never drop a row).</summary>
public class AuditLogEntryModel
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

/// <summary>A keyset page of audit entries plus the cursor for the next page (null when exhausted).</summary>
public class AuditLogPageModel
{
    public List<AuditLogEntryModel> Entries { get; set; } = new();

    /// <summary>The cursor to pass as <c>Cursor</c> for the next page, or null when there are no more rows.</summary>
    public int? NextCursor { get; set; }
}
