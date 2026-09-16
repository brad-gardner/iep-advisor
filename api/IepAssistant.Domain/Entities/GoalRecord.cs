namespace IepAssistant.Domain.Entities;

/// <summary>
/// A goal persisted as a first-class record across documents/years (plan 7, decision 6), projected from
/// the Goals table field of a finalized <see cref="AuthoredDocumentVersion"/> — see
/// <c>GoalRecordService.ProjectOnFinalizeAsync</c>, called from
/// <c>AuthoredDocumentVersionService.FinalizeAsync</c> inside its finalize transaction.
///
/// <para><see cref="LineageId"/> is the goal row's stable <c>_rowId</c> (see
/// <see cref="RowMetaKeys.RowId"/> equivalent in <c>IepAssistant.Services.Models.RowMetaKeys</c>): the
/// same goal carried forward into a later finalized version keeps the same LineageId across separate
/// GoalRecord rows. By construction, exactly one GoalRecord per LineageId ever holds a non-terminal
/// status (<see cref="GoalRecordStatus.Active"/>/<see cref="GoalRecordStatus.Met"/>/
/// <see cref="GoalRecordStatus.NotMet"/>) at a time — the "current" record for that lineage. Every prior
/// record for a carried-forward lineage becomes <see cref="GoalRecordStatus.Carried"/>; a lineage dropped
/// from the new version becomes <see cref="GoalRecordStatus.Retired"/> (no new record is inserted for
/// it).</para>
/// </summary>
public class GoalRecord : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }

    /// <summary>Stable identity of the goal across versions — the row's <c>_rowId</c>.</summary>
    public Guid LineageId { get; set; }

    public int AuthoredDocumentVersionId { get; set; }

    /// <summary>The Draft instance this record was projected from at finalize time (provenance only).</summary>
    public int DocumentInstanceId { get; set; }

    /// <summary>The Goals table field's FieldKey on the pinned template version this row came from.</summary>
    public Guid FieldKey { get; set; }

    public string? Domain { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }

    public GoalRecordStatus Status { get; set; } = GoalRecordStatus.Active;

    /// <summary>Set when a human resolves the status (NotMet requires one) or by the finalize
    /// projection when retiring a lineage (from a matching <see cref="GoalRetirement"/>, else a default
    /// message).</summary>
    public string? StatusReason { get; set; }

    /// <summary>Last time a human reviewed/updated the status.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>When this record was created by the finalize projection.</summary>
    public DateTime ProjectedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public AuthoredDocumentVersion AuthoredDocumentVersion { get; set; } = null!;
    public DocumentInstance DocumentInstance { get; set; } = null!;
    public ICollection<GoalObservation> Observations { get; set; } = new List<GoalObservation>();
}
