using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Goals as first-class records across documents/years, plus provider progress observations
/// (see <see cref="IGoalRecordService"/>, plan 7 decision 6).
/// </summary>
public class GoalRecordService : IGoalRecordService
{
    private const string PermissionMessage = "You do not have permission to access this student's goals.";
    private const string GoalNotFoundMessage = "Goal not found.";
    private const string InstanceNotFoundMessage = "Document not found.";
    private const string DefaultRetirementReason = "Removed from the document";

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAccessService _accessService;
    private readonly ILogger<GoalRecordService> _logger;

    public GoalRecordService(
        ApplicationDbContext context, IOrgAccessService orgAccess, IAccessService accessService, ILogger<GoalRecordService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _accessService = accessService;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Finalize projection

    public async Task ProjectOnFinalizeAsync(DocumentInstance instance, AuthoredDocumentVersion version, CancellationToken ct = default)
    {
        var sections = await _context.TemplateSections
            .Where(s => s.DocumentTemplateVersionId == instance.DocumentTemplateVersionId)
            .Include(s => s.Fields)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        var semantics = TemplateSemanticsReader.Read(sections);
        if (!semantics.TryGetValue(FieldSemantics.Goals, out var goalsField))
            return; // this template has no Goals table — nothing to project.

        var values = ValueDocumentJson.Parse(version.ValuesJson);
        var rows = values[goalsField.FieldKey.ToString()] as JsonArray;

        var previousVersionId = await _context.AuthoredDocumentVersions
            .Where(v => v.SchoolStudentId == version.SchoolStudentId
                        && v.DocumentTypeId == version.DocumentTypeId
                        && v.VersionNumber < version.VersionNumber)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync(ct);

        var priorRecords = previousVersionId.HasValue
            ? await _context.GoalRecords.Where(g => g.AuthoredDocumentVersionId == previousVersionId.Value).ToListAsync(ct)
            : new List<GoalRecord>();

        var now = DateTime.UtcNow;
        var newLineageIds = new HashSet<Guid>();

        string? Cell(JsonObject row, string colSemantic) =>
            goalsField.Columns.TryGetValue(colSemantic, out var colKey) ? DraftRowLabeler.CellText(row, colKey) : null;

        if (rows != null)
        {
            foreach (var row in rows.OfType<JsonObject>())
            {
                var rowIdText = row[RowMetaKeys.RowId]?.ToString();
                if (!Guid.TryParse(rowIdText, out var lineageId) || !newLineageIds.Add(lineageId))
                    continue; // malformed or duplicate row id — defensive; DocumentInstanceService already guarantees uniqueness.

                await _context.GoalRecords.AddAsync(new GoalRecord
                {
                    SchoolStudentId = version.SchoolStudentId,
                    LineageId = lineageId,
                    AuthoredDocumentVersionId = version.Id,
                    DocumentInstanceId = instance.Id,
                    FieldKey = goalsField.FieldKey,
                    Domain = Cell(row, ColumnSemantics.Domain),
                    GoalText = Cell(row, ColumnSemantics.GoalText) ?? string.Empty,
                    Baseline = Cell(row, ColumnSemantics.Baseline),
                    TargetCriteria = Cell(row, ColumnSemantics.TargetCriteria),
                    MeasurementMethod = Cell(row, ColumnSemantics.MeasurementMethod),
                    Timeframe = Cell(row, ColumnSemantics.Timeframe),
                    Status = GoalRecordStatus.Active,
                    ProjectedAt = now,
                    CreatedById = version.FinalizedByUserId,
                    UpdatedById = version.FinalizedByUserId
                }, ct);
            }
        }

        if (priorRecords.Count > 0)
        {
            var retirementReasons = (await _context.GoalRetirements.AsNoTracking()
                    .Where(r => r.DocumentInstanceId == instance.Id)
                    .ToListAsync(ct))
                .GroupBy(r => r.LineageId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RetiredAt).First().Reason);

            foreach (var prior in priorRecords)
            {
                if (newLineageIds.Contains(prior.LineageId))
                {
                    prior.Status = GoalRecordStatus.Carried;
                    prior.StatusReason = null;
                }
                else
                {
                    prior.Status = GoalRecordStatus.Retired;
                    prior.StatusReason = retirementReasons.TryGetValue(prior.LineageId, out var reason) ? reason : DefaultRetirementReason;
                }
                prior.UpdatedById = version.FinalizedByUserId;
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Projected {NewCount} goal record(s) for AuthoredDocumentVersion {VersionId} (student {StudentId}); {PriorCount} prior lineage record(s) carried/retired.",
            newLineageIds.Count, version.Id, version.SchoolStudentId, priorRecords.Count);
    }

    // ---------------------------------------------------------------- Reads

    public async Task<ServiceResult<List<GoalRecordModel>>> GetForStudentAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<GoalRecordModel>>.FailureResult(PermissionMessage);

        var records = await LoadCurrentAsync(g => g.SchoolStudentId == schoolStudentId, ct);
        return ServiceResult<List<GoalRecordModel>>.SuccessResult(records);
    }

    public async Task<ServiceResult<List<GoalLineageModel>>> GetHistoryForStudentAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<GoalLineageModel>>.FailureResult(PermissionMessage);

        var all = await _context.GoalRecords.AsNoTracking()
            .Where(g => g.SchoolStudentId == schoolStudentId)
            .Include(g => g.AuthoredDocumentVersion).ThenInclude(v => v.DocumentType)
            .Include(g => g.Observations)
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        var lineages = all
            .GroupBy(g => g.LineageId)
            .Select(grp => new GoalLineageModel
            {
                LineageId = grp.Key,
                Records = grp.OrderByDescending(g => g.AuthoredDocumentVersion.VersionNumber).Select(g => MapRecord(g, today)).ToList()
            })
            .OrderByDescending(l => l.Records.Count == 0 ? 0 : l.Records.Max(r => r.VersionNumber))
            .ToList();

        return ServiceResult<List<GoalLineageModel>>.SuccessResult(lineages);
    }

    public async Task<ServiceResult<List<GoalRecordModel>>> GetForChildAsync(int userId, int childId, CancellationToken ct = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
            return ServiceResult<List<GoalRecordModel>>.FailureResult(PermissionMessage);

        var linkedStudentIds = await _context.ChildLinks.AsNoTracking()
            .Where(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId)
            .ToListAsync(ct);
        if (linkedStudentIds.Count == 0)
            return ServiceResult<List<GoalRecordModel>>.SuccessResult(new List<GoalRecordModel>());

        var records = await LoadCurrentAsync(g => linkedStudentIds.Contains(g.SchoolStudentId), ct);
        return ServiceResult<List<GoalRecordModel>>.SuccessResult(records);
    }

    // ---------------------------------------------------------------- Writes

    public async Task<ServiceResult<GoalObservationModel>> AddObservationAsync(
        int userId, int goalRecordId, CreateGoalObservationModel model, CancellationToken ct = default)
    {
        if (model.Value == null && string.IsNullOrWhiteSpace(model.Note))
            return ServiceResult<GoalObservationModel>.FailureResult("Enter a value or a note.");

        var record = await _context.GoalRecords.FirstOrDefaultAsync(g => g.Id == goalRecordId, ct);
        if (record == null)
            return ServiceResult<GoalObservationModel>.FailureResult(GoalNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(userId, record.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<GoalObservationModel>.FailureResult(PermissionMessage);

        var observation = new GoalObservation
        {
            GoalRecordId = goalRecordId,
            ObservedAt = model.ObservedAt ?? DateTime.UtcNow,
            Value = model.Value,
            Unit = model.Unit,
            Note = model.Note,
            RecordedByUserId = userId,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.GoalObservations.AddAsync(observation, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<GoalObservationModel>.SuccessResult(MapObservation(observation));
    }

    public async Task<ServiceResult<GoalRecordModel>> UpdateStatusAsync(
        int userId, int goalRecordId, UpdateGoalStatusModel model, CancellationToken ct = default)
    {
        if (model.Status is not (GoalRecordStatus.Active or GoalRecordStatus.Met or GoalRecordStatus.NotMet))
            return ServiceResult<GoalRecordModel>.FailureResult("Status must be Active, Met, or NotMet.");
        if (model.Status == GoalRecordStatus.NotMet && string.IsNullOrWhiteSpace(model.Reason))
            return ServiceResult<GoalRecordModel>.FailureResult("A reason is required when marking a goal Not Met.");

        var record = await _context.GoalRecords
            .Include(g => g.AuthoredDocumentVersion).ThenInclude(v => v.DocumentType)
            .Include(g => g.Observations)
            .FirstOrDefaultAsync(g => g.Id == goalRecordId, ct);
        if (record == null)
            return ServiceResult<GoalRecordModel>.FailureResult(GoalNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(userId, record.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<GoalRecordModel>.FailureResult(PermissionMessage);

        record.Status = model.Status;
        record.StatusReason = model.Status == GoalRecordStatus.NotMet ? model.Reason!.Trim() : model.Reason?.Trim();
        record.ReviewedAt = DateTime.UtcNow;
        record.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<GoalRecordModel>.SuccessResult(MapRecord(record, DateTime.UtcNow.Date));
    }

    public async Task<ServiceResult> RecordRetirementAsync(
        int userId, int documentInstanceId, CreateGoalRetirementModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Reason))
            return ServiceResult.FailureResult("A reason is required to remove a goal.");

        var instance = await _context.DocumentInstances.AsNoTracking()
            .Where(i => i.Id == documentInstanceId)
            .Select(i => new { i.SchoolStudentId, i.Status })
            .FirstOrDefaultAsync(ct);
        if (instance == null)
            return ServiceResult.FailureResult(InstanceNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(userId, instance.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(PermissionMessage);

        if (instance.Status is not (DocumentInstanceStatus.Draft or DocumentInstanceStatus.Finalizing))
            return ServiceResult.FailureResult("This document can no longer be edited.");

        await _context.GoalRetirements.AddAsync(new GoalRetirement
        {
            DocumentInstanceId = documentInstanceId,
            LineageId = model.LineageId,
            Reason = model.Reason.Trim(),
            RetiredByUserId = userId,
            RetiredAt = DateTime.UtcNow,
            CreatedById = userId,
            UpdatedById = userId
        }, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Mapping

    private async Task<List<GoalRecordModel>> LoadCurrentAsync(System.Linq.Expressions.Expression<Func<GoalRecord, bool>> predicate, CancellationToken ct)
    {
        var records = await _context.GoalRecords.AsNoTracking()
            .Where(predicate)
            .Where(g => g.Status == GoalRecordStatus.Active || g.Status == GoalRecordStatus.Met || g.Status == GoalRecordStatus.NotMet)
            .Include(g => g.AuthoredDocumentVersion).ThenInclude(v => v.DocumentType)
            .OrderBy(g => g.Domain).ThenBy(g => g.GoalText)
            .ToListAsync(ct);
        if (records.Count == 0)
            return new List<GoalRecordModel>();

        // A goal's progress belongs to its LINEAGE, not to the record a particular finalize produced:
        // observations logged against the goal before it was carried into an amendment or a new IEP
        // stay on its trajectory. One query over every record of the lineages in view.
        var lineageKeys = records.Select(g => (g.SchoolStudentId, g.LineageId)).ToHashSet();
        var studentIds = lineageKeys.Select(k => k.SchoolStudentId).Distinct().ToList();
        var lineageIds = lineageKeys.Select(k => k.LineageId).Distinct().ToList();
        var observations = await _context.GoalObservations.AsNoTracking()
            .Where(o => studentIds.Contains(o.GoalRecord.SchoolStudentId) && lineageIds.Contains(o.GoalRecord.LineageId))
            .Select(o => new { o.GoalRecord.SchoolStudentId, o.GoalRecord.LineageId, Observation = o })
            .ToListAsync(ct);
        var byLineage = observations
            .Where(x => lineageKeys.Contains((x.SchoolStudentId, x.LineageId)))
            .ToLookup(x => (x.SchoolStudentId, x.LineageId), x => x.Observation);

        var today = DateTime.UtcNow.Date;
        return records.Select(g => MapRecord(g, today, byLineage[(g.SchoolStudentId, g.LineageId)].ToList())).ToList();
    }

    private static GoalRecordModel MapRecord(GoalRecord g, DateTime today) => MapRecord(g, today, g.Observations.ToList());

    private static GoalRecordModel MapRecord(GoalRecord g, DateTime today, List<GoalObservation> lineageObservations)
    {
        var orderedObservations = lineageObservations.OrderBy(o => o.ObservedAt).ToList();
        var lastObservedAt = orderedObservations.Count > 0 ? orderedObservations[^1].ObservedAt : (DateTime?)null;
        var referenceDate = lastObservedAt ?? g.ProjectedAt;
        var numericPoints = orderedObservations
            .Where(o => o.Value.HasValue)
            .TakeLast(GoalRecordRules.TrajectoryMaxPoints)
            .Select(o => new GoalTrajectoryPointModel { ObservedAt = o.ObservedAt, Value = o.Value!.Value })
            .ToList();

        return new GoalRecordModel
        {
            Id = g.Id,
            LineageId = g.LineageId,
            VersionId = g.AuthoredDocumentVersionId,
            VersionNumber = g.AuthoredDocumentVersion.VersionNumber,
            DocumentTypeKey = g.AuthoredDocumentVersion.DocumentType.Key,
            Domain = g.Domain,
            GoalText = g.GoalText,
            Baseline = g.Baseline,
            TargetCriteria = g.TargetCriteria,
            MeasurementMethod = g.MeasurementMethod,
            Timeframe = g.Timeframe,
            Status = g.Status,
            StatusReason = g.StatusReason,
            ReviewedAt = g.ReviewedAt,
            ProjectedAt = g.ProjectedAt,
            LastObservedAt = lastObservedAt,
            StaleAfterDays = GoalRecordRules.StaleAfterDays,
            IsStale = GoalRecordRules.IsStale(referenceDate, today),
            Observations = orderedObservations.TakeLast(GoalRecordRules.TrajectoryMaxPoints).Select(MapObservation).ToList(),
            Trajectory = new GoalTrajectoryModel
            {
                Points = numericPoints,
                InsufficientData = numericPoints.Count < GoalRecordRules.MinNumericPointsForTrajectory
            }
        };
    }

    private static GoalObservationModel MapObservation(GoalObservation o) => new()
    {
        Id = o.Id,
        GoalRecordId = o.GoalRecordId,
        ObservedAt = o.ObservedAt,
        Value = o.Value,
        Unit = o.Unit,
        Note = o.Note,
        RecordedByUserId = o.RecordedByUserId
    };
}
