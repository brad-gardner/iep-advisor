using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Builds the role-filtered evidence bundle that grounds prefill and AI assist for one student.
/// Sources, in this order: the student record and team; the latest finalized authored version per
/// document type (goals/services/accommodations/transition rows via semantics, present levels, ETR
/// findings); the latest legacy typed IEP version when no authored IEP exists; the student's
/// explicitly shareable workspace entries; and family notes the parent explicitly shared. Parent prep
/// notes, analyses, advocacy goals and unshared student entries are never included — the bundle is
/// built with the acting staff member's access and nothing more.
/// </summary>
public sealed class StudentEvidenceService : IStudentEvidenceService
{
    private const string PermissionMessage = "You do not have permission to view this student.";
    private static readonly Regex TagStripper = new("<[^>]+>", RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IStudentWorkspaceService _workspace;
    private readonly IParentContributionService _contributions;
    private readonly IAuditLogger _audit;

    public StudentEvidenceService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IStudentWorkspaceService workspace,
        IParentContributionService contributions,
        IAuditLogger audit)
    {
        _context = context;
        _orgAccess = orgAccess;
        _workspace = workspace;
        _contributions = contributions;
        _audit = audit;
    }

    public async Task<ServiceResult<StudentEvidenceBundle>> BuildForStaffAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<StudentEvidenceBundle>.FailureResult(PermissionMessage);

        var items = new List<EvidenceItem>();
        var sources = new List<EvidenceSource>();
        var seq = 0;
        string NextId() => $"E{++seq}";

        // ---- Identity + team
        var student = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == schoolStudentId)
            .Select(s => new
            {
                s.Id, s.FirstName, s.LastName, s.DateOfBirth, s.GradeLevel, s.DisabilityCategory, s.LegacyDisabilityText,
                SchoolName = s.School.Name, DistrictName = s.School.District.Name,
                State = s.StateCode ?? s.School.StateCode ?? s.School.District.StateCode
            })
            .FirstOrDefaultAsync(ct);
        if (student == null)
            return ServiceResult<StudentEvidenceBundle>.FailureResult("Student not found.");

        var identity = new List<string> { $"Name: {student.FirstName} {student.LastName}".Trim() };
        if (student.DateOfBirth is { } dob) identity.Add($"Date of birth: {dob:yyyy-MM-dd} (age {Age(dob)})");
        var gradeText = student.GradeLevel?.ToDisplay() ?? "";
        var disabilityText = student.DisabilityCategory == DisabilityCategory.Other && !string.IsNullOrWhiteSpace(student.LegacyDisabilityText)
            ? student.LegacyDisabilityText
            : student.DisabilityCategory?.ToDisplay() ?? "";
        if (gradeText.Length > 0) identity.Add($"Grade: {gradeText}");
        if (disabilityText.Length > 0) identity.Add($"Disability category: {disabilityText}");
        identity.Add($"School: {student.SchoolName} ({student.DistrictName}{(student.State != null ? ", " + student.State : "")})");
        items.Add(new EvidenceItem
        {
            Id = NextId(), Kind = EvidenceKind.Identity, SourceType = "SchoolStudent", SourceId = student.Id,
            SourceLabel = "Student record", AuthorRole = "school", Text = string.Join("\n", identity),
            Fields = new Dictionary<string, string>
            {
                ["name"] = $"{student.FirstName} {student.LastName}".Trim(),
                ["dateOfBirth"] = student.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
                ["grade"] = gradeText,
                ["disabilityCategory"] = disabilityText,
                ["school"] = student.SchoolName,
                ["district"] = student.DistrictName,
            }
        });

        // Team: functional roles from the IEP team table (plan 3), lead first; older students with no
        // team rows fall back to the access rows so the bundle never loses its "who is involved" items.
        var team = await _context.StudentTeamMembers.AsNoTracking()
            .Where(m => m.SchoolStudentId == schoolStudentId && m.IsActive)
            .OrderByDescending(m => m.IsLead).ThenBy(m => m.User.LastName).ThenBy(m => m.User.FirstName)
            .Select(m => new { m.Id, m.TeamRole, m.IsLead, m.User.FirstName, m.User.LastName })
            .ToListAsync(ct);
        if (team.Count > 0)
        {
            foreach (var m in team)
            {
                items.Add(new EvidenceItem
                {
                    Id = NextId(), Kind = EvidenceKind.TeamMember, SourceType = "StudentTeamMember", SourceId = m.Id,
                    SourceLabel = "IEP team", AuthorRole = "school",
                    Text = $"{m.FirstName} {m.LastName} — {m.TeamRole.ToDisplay()}{(m.IsLead ? " (lead)" : "")}".Trim()
                });
            }
        }
        else
        {
            var grants = await _context.SchoolStudentAccesses.AsNoTracking()
                .Where(a => a.SchoolStudentId == schoolStudentId && a.IsActive)
                .Select(a => new
                {
                    a.Id, a.Role, a.User.FirstName, a.User.LastName,
                    Title = _context.StaffProfiles.Where(p => p.UserId == a.UserId && p.IsActive).Select(p => p.Title ?? p.OrgRole.Name).FirstOrDefault()
                })
                .ToListAsync(ct);
            foreach (var m in grants)
            {
                items.Add(new EvidenceItem
                {
                    Id = NextId(), Kind = EvidenceKind.TeamMember, SourceType = "SchoolStudentAccess", SourceId = m.Id,
                    SourceLabel = "IEP team", AuthorRole = "school",
                    Text = $"{m.FirstName} {m.LastName} — {m.Title ?? m.Role.ToString()}".Trim()
                });
            }
        }

        // ---- Latest finalized authored version per document type
        var latestVersions = await _context.AuthoredDocumentVersions.AsNoTracking()
            .Where(v => v.SchoolStudentId == schoolStudentId)
            .GroupBy(v => v.DocumentTypeId)
            .Select(g => g.OrderByDescending(v => v.VersionNumber).First())
            .ToListAsync(ct);
        var typeKeys = await _context.DocumentTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Key, ct);

        var hasAuthoredIep = false;
        foreach (var version in latestVersions.OrderBy(v => v.DocumentTypeId))
        {
            var typeKey = typeKeys.GetValueOrDefault(version.DocumentTypeId, "Document");
            if (typeKey == "IEP") hasAuthoredIep = true;
            var label = $"{typeKey} v{version.VersionNumber}";
            sources.Add(new EvidenceSource { SourceType = "AuthoredDocumentVersion", SourceId = version.Id, Label = label, Date = version.FinalizedAt, DocumentTypeKey = typeKey });

            var sections = await _context.TemplateSections.AsNoTracking()
                .Where(s => s.DocumentTemplateVersionId == version.DocumentTemplateVersionId)
                .Include(s => s.Fields)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync(ct);
            var semantics = TemplateSemanticsReader.Read(sections);
            JsonObject values;
            try { values = JsonNode.Parse(version.ValuesJson) as JsonObject ?? new JsonObject(); }
            catch (JsonException) { values = new JsonObject(); }

            void AddRows(string fieldSemantic, EvidenceKind kind, string primary)
            {
                if (!semantics.TryGetValue(fieldSemantic, out var field)) return;
                if (values[field.FieldKey.ToString()] is not JsonArray rows) return;
                foreach (var row in rows.OfType<JsonObject>())
                {
                    var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (colSem, colKey) in field.Columns)
                    {
                        var cell = row[colKey.ToString()];
                        var text = cell is JsonValue v ? v.ToString() : cell?.ToJsonString();
                        if (!string.IsNullOrWhiteSpace(text)) fields[colSem] = text!;
                    }
                    if (fields.Count == 0) continue;
                    var rowId = row[RowMetaKeys.RowId]?.ToString();
                    var headline = fields.GetValueOrDefault(primary) ?? fields.Values.First();
                    var detail = string.Join(" | ", fields.Where(kv => kv.Key != primary).Select(kv => $"{kv.Key}: {kv.Value}"));
                    items.Add(new EvidenceItem
                    {
                        Id = NextId(), Kind = kind, SourceType = "AuthoredDocumentVersion", SourceId = version.Id,
                        SourceLabel = label, SourceDate = version.FinalizedAt, AuthorRole = "school",
                        Text = detail.Length > 0 ? $"{headline} ({detail})" : headline,
                        RowId = rowId, Fields = fields
                    });
                }
            }

            void AddNarrative(string fieldSemantic, EvidenceKind kind)
            {
                if (!semantics.TryGetValue(fieldSemantic, out var field)) return;
                var text = values[field.FieldKey.ToString()] is JsonValue v ? StripHtml(v.ToString()) : null;
                if (string.IsNullOrWhiteSpace(text)) return;
                items.Add(new EvidenceItem
                {
                    Id = NextId(), Kind = kind, SourceType = "AuthoredDocumentVersion", SourceId = version.Id,
                    SourceLabel = $"{label} — {field.Label}", SourceDate = version.FinalizedAt, AuthorRole = "school",
                    Text = text, Fields = new Dictionary<string, string> { ["semantic"] = fieldSemantic }
                });
            }

            if (typeKey == "ETR")
            {
                AddNarrative(FieldSemantics.TeamSummary, EvidenceKind.EtrFinding);
                AddNarrative(FieldSemantics.PresentLevels, EvidenceKind.EtrFinding);
                AddNarrative(FieldSemantics.EligibilityDetermination, EvidenceKind.EtrFinding);
                AddNarrative(FieldSemantics.Eligibility, EvidenceKind.EtrFinding);
                AddRows(FieldSemantics.EvaluatorReports, EvidenceKind.EtrFinding, ColumnSemantics.Findings);
            }
            else
            {
                AddNarrative(FieldSemantics.PresentLevels, EvidenceKind.PresentLevels);
                AddRows(FieldSemantics.Goals, EvidenceKind.PriorGoal, ColumnSemantics.GoalText);
                AddRows(FieldSemantics.Services, EvidenceKind.PriorService, ColumnSemantics.ServiceType);
                AddRows(FieldSemantics.Accommodations, EvidenceKind.PriorAccommodation, ColumnSemantics.Accommodation);
                AddRows(FieldSemantics.Transition, EvidenceKind.PriorTransition, ColumnSemantics.GoalArea);
            }
        }

        // ---- Legacy typed IEP fallback (only when no authored IEP exists)
        if (!hasAuthoredIep)
        {
            var legacy = await _context.IepVersions.AsNoTracking()
                .Where(v => v.SchoolStudentId == schoolStudentId)
                .OrderByDescending(v => v.VersionNumber)
                .Include(v => v.Sections).Include(v => v.Goals).Include(v => v.ServiceLines).Include(v => v.Accommodations)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);
            if (legacy != null)
            {
                var label = $"IEP v{legacy.VersionNumber} (legacy)";
                sources.Add(new EvidenceSource { SourceType = "IepVersion", SourceId = legacy.Id, Label = label, Date = legacy.FinalizedAt, DocumentTypeKey = "IEP" });
                var plaafp = legacy.Sections.FirstOrDefault(s => s.SectionKind == IepSectionKind.PresentLevels);
                if (plaafp != null && !string.IsNullOrWhiteSpace(plaafp.RichText))
                    items.Add(new EvidenceItem { Id = NextId(), Kind = EvidenceKind.PresentLevels, SourceType = "IepVersion", SourceId = legacy.Id, SourceLabel = $"{label} — Present Levels", SourceDate = legacy.FinalizedAt, AuthorRole = "school", Text = StripHtml(plaafp.RichText) });
                foreach (var g in legacy.Goals.OrderBy(g => g.DisplayOrder))
                {
                    var fields = new Dictionary<string, string>();
                    if (!string.IsNullOrWhiteSpace(g.Domain)) fields[ColumnSemantics.Domain] = g.Domain;
                    if (!string.IsNullOrWhiteSpace(g.GoalText)) fields[ColumnSemantics.GoalText] = g.GoalText;
                    if (!string.IsNullOrWhiteSpace(g.Baseline)) fields[ColumnSemantics.Baseline] = g.Baseline;
                    if (!string.IsNullOrWhiteSpace(g.TargetCriteria)) fields[ColumnSemantics.TargetCriteria] = g.TargetCriteria;
                    if (!string.IsNullOrWhiteSpace(g.MeasurementMethod)) fields[ColumnSemantics.MeasurementMethod] = g.MeasurementMethod;
                    if (!string.IsNullOrWhiteSpace(g.Timeframe)) fields[ColumnSemantics.Timeframe] = g.Timeframe;
                    if (fields.Count == 0) continue;
                    items.Add(new EvidenceItem { Id = NextId(), Kind = EvidenceKind.PriorGoal, SourceType = "IepVersion", SourceId = legacy.Id, SourceLabel = label, SourceDate = legacy.FinalizedAt, AuthorRole = "school", Text = g.GoalText ?? fields.Values.First(), RowId = g.LineageId.ToString(), Fields = fields });
                }
                foreach (var sl in legacy.ServiceLines.OrderBy(s => s.DisplayOrder))
                {
                    var fields = new Dictionary<string, string>();
                    if (!string.IsNullOrWhiteSpace(sl.ServiceType)) fields[ColumnSemantics.ServiceType] = sl.ServiceType;
                    if (!string.IsNullOrWhiteSpace(sl.Frequency)) fields[ColumnSemantics.Frequency] = sl.Frequency;
                    if (!string.IsNullOrWhiteSpace(sl.Duration)) fields[ColumnSemantics.Duration] = sl.Duration;
                    if (!string.IsNullOrWhiteSpace(sl.Location)) fields[ColumnSemantics.Location] = sl.Location;
                    if (!string.IsNullOrWhiteSpace(sl.ProviderRole)) fields[ColumnSemantics.ProviderRole] = sl.ProviderRole;
                    if (fields.Count == 0) continue;
                    items.Add(new EvidenceItem { Id = NextId(), Kind = EvidenceKind.PriorService, SourceType = "IepVersion", SourceId = legacy.Id, SourceLabel = label, SourceDate = legacy.FinalizedAt, AuthorRole = "school", Text = sl.ServiceType ?? fields.Values.First(), RowId = sl.LineageId.ToString(), Fields = fields });
                }
                foreach (var a in legacy.Accommodations.OrderBy(a => a.DisplayOrder))
                {
                    if (string.IsNullOrWhiteSpace(a.Text)) continue;
                    var fields = new Dictionary<string, string> { [ColumnSemantics.Accommodation] = a.Text };
                    if (!string.IsNullOrWhiteSpace(a.Category)) fields[ColumnSemantics.Category] = a.Category;
                    items.Add(new EvidenceItem { Id = NextId(), Kind = EvidenceKind.PriorAccommodation, SourceType = "IepVersion", SourceId = legacy.Id, SourceLabel = label, SourceDate = legacy.FinalizedAt, AuthorRole = "school", Text = a.Text, RowId = a.LineageId.ToString(), Fields = fields });
                }
            }
        }

        // ---- Student voice (explicitly shareable entries only)
        var voice = await _workspace.GetShareableEntriesForSchoolStudentAsync(userId, schoolStudentId, ct);
        if (voice.Success && voice.Data != null)
        {
            foreach (var e in voice.Data)
            {
                items.Add(new EvidenceItem
                {
                    Id = NextId(), Kind = EvidenceKind.StudentVoice, SourceType = "StudentWorkspaceEntry", SourceId = e.Id,
                    SourceLabel = $"Student — {e.EntryKind}", SourceDate = e.UpdatedAt, AuthorRole = "student", Text = e.Content
                });
            }
        }

        // ---- Family notes the parent explicitly shared
        var family = await _contributions.GetSharedForSchoolStudentAsync(userId, schoolStudentId, ct);
        if (family.Success && family.Data != null)
        {
            foreach (var c in family.Data)
            {
                items.Add(new EvidenceItem
                {
                    Id = NextId(), Kind = EvidenceKind.ParentContribution, SourceType = "ParentContribution", SourceId = c.Id,
                    SourceLabel = $"Family — {c.Kind}", SourceDate = c.UpdatedAt, AuthorRole = "family", Text = c.Text
                });
            }
        }

        // Access trail (FERPA): the bundle is an aggregate read of the student record and of every
        // finalized version whose content it carries — record it the same way the direct reads do.
        _audit.Record(AuditAction.View, userId, "StudentEvidence", schoolStudentId);
        foreach (var source in sources)
            _audit.Record(AuditAction.View, userId, source.SourceType, source.SourceId);

        return ServiceResult<StudentEvidenceBundle>.SuccessResult(new StudentEvidenceBundle
        {
            SchoolStudentId = schoolStudentId, Items = items, Sources = sources
        });
    }

    private static int Age(DateTime dob)
    {
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age)) age--;
        return age;
    }

    private static string StripHtml(string html)
        => System.Net.WebUtility.HtmlDecode(TagStripper.Replace(html.Replace("</p>", "\n").Replace("<br>", "\n").Replace("<br/>", "\n"), string.Empty)).Trim();
}
