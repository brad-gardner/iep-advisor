using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 7, decision 2: the pre-meeting brief's deterministic resource-commitment detection (new/changed
/// Services rows; scalar fields whose label matches the placement/ESY/transportation/1:1 keyword set,
/// only when actually changed vs. the prior finalized version) and procedural checklist (a missing
/// required participant is unsatisfied; notice timing reads the actual MeetingScheduled notification;
/// family input reflects ParentContribution/OfflineFamilyInput/DraftResponse activity). The cached brief
/// round-trips through Generate (write) and Get (read).
/// </summary>
public sealed class MeetingBriefServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private sealed class FakeClaudeClient : IClaudeClient
    {
        public string? CannedResponse { get; set; } = "The team is proposing an additional OT service and a placement change.";
        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CannedResponse);
    }

    private MeetingBriefService CreateService(ApplicationDbContext ctx, IClaudeClient? claude = null) => new(
        ctx,
        new OrgAccessService(ctx),
        claude ?? new FakeClaudeClient(),
        new DraftResponseService(ctx, new AccessService(ctx), new OrgAccessService(ctx), new NotificationService(ctx), NullLogger<DraftResponseService>.Instance),
        NullLogger<MeetingBriefService>.Instance);

    private static string Cfg<T>(T config) => JsonSerializer.Serialize(config, TemplateFieldConfigValidator.JsonOptions);

    private sealed record TemplateKeys(int VersionId, Guid ServicesKey, Guid ServiceTypeCol, Guid FrequencyCol, Guid PlacementKey, Guid EsyKey);

    private TemplateKeys SeedTemplate(int docTypeId)
    {
        var servicesKey = Guid.NewGuid();
        var serviceTypeCol = Guid.NewGuid();
        var frequencyCol = Guid.NewGuid();
        var placementKey = Guid.NewGuid();
        var esyKey = Guid.NewGuid();

        using var ctx = _db.Context();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        var template = new DocumentTemplate { DocumentTypeId = docTypeId, Name = "T", Versions = { version } };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();

        var servicesConfig = Cfg(new TableFieldConfig
        {
            Semantic = FieldSemantics.Services,
            Columns = new List<TableColumn>
            {
                new() { ColumnKey = serviceTypeCol, Type = FieldType.Text, Label = "Service", Required = true, Semantic = ColumnSemantics.ServiceType },
                new() { ColumnKey = frequencyCol, Type = FieldType.Text, Label = "Frequency", Required = false, Semantic = ColumnSemantics.Frequency }
            }
        });

        var section = new TemplateSection
        {
            DocumentTemplateVersionId = version.Id,
            SectionKey = Guid.NewGuid(),
            Title = "Services & Placement",
            DisplayOrder = 0,
            Fields =
            {
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = servicesKey, FieldType = FieldType.Table, Label = "Services", Required = false, ConfigJson = servicesConfig, DisplayOrder = 0 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = placementKey, FieldType = FieldType.Text, Label = "Placement", Required = false, DisplayOrder = 1 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = esyKey, FieldType = FieldType.Text, Label = "Extended School Year (ESY)", Required = false, DisplayOrder = 2 }
            }
        };
        ctx.TemplateSections.Add(section);
        ctx.SaveChanges();

        return new TemplateKeys(version.Id, servicesKey, serviceTypeCol, frequencyCol, placementKey, esyKey);
    }

    [Fact]
    public async Task GenerateAsync_DetectsNewServiceAndPlacementEsyChanges_AndChecklist()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var studentId = _db.Student(schoolId, "Sam");
        var (userId, _) = _db.Staff("lea@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);

        var keys = SeedTemplate(docTypeId: 1);
        var speechRowId = Guid.NewGuid();
        var otRowId = Guid.NewGuid();

        var priorValues = $$"""
        {
          "{{keys.ServicesKey}}": [ { "_rowId": "{{speechRowId}}", "{{keys.ServiceTypeCol}}": "Speech", "{{keys.FrequencyCol}}": "1x/week" } ],
          "{{keys.PlacementKey}}": "General education",
          "{{keys.EsyKey}}": ""
        }
        """;
        var draftValues = $$"""
        {
          "{{keys.ServicesKey}}": [
            { "_rowId": "{{speechRowId}}", "{{keys.ServiceTypeCol}}": "Speech", "{{keys.FrequencyCol}}": "1x/week" },
            { "_rowId": "{{otRowId}}", "{{keys.ServiceTypeCol}}": "OT", "{{keys.FrequencyCol}}": "2x/week" }
          ],
          "{{keys.PlacementKey}}": "Resource room",
          "{{keys.EsyKey}}": "Yes - summer services"
        }
        """;

        int instanceId;
        using (var ctx = _db.Context())
        {
            var version = new AuthoredDocumentVersion
            {
                SchoolStudentId = studentId,
                DocumentTypeId = 1,
                DocumentTemplateVersionId = keys.VersionId,
                VersionNumber = 1,
                ValuesJson = priorValues,
                FinalizedByUserId = userId,
                FinalizedAt = DateTime.UtcNow.AddDays(-30)
            };
            ctx.AuthoredDocumentVersions.Add(version);

            var instance = new DocumentInstance
            {
                SchoolStudentId = studentId,
                DocumentTypeId = 1,
                DocumentTemplateVersionId = keys.VersionId,
                Status = DocumentInstanceStatus.Draft,
                ValuesJson = draftValues
            };
            ctx.DocumentInstances.Add(instance);
            ctx.SaveChanges();
            instanceId = instance.Id;
        }

        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(3), status: MeetingStatus.Scheduled);
        using (var ctx = _db.Context())
        {
            ctx.Meetings.Single(m => m.Id == meetingId).DocumentInstanceId = instanceId;
            ctx.SaveChanges();
        }

        var requiredAttendedId = _db.MeetingParticipant(meetingId, userId, isRequired: true);
        var missingUserId = _db.SeedUser("absent@example.com", UserRole.Educator);
        var requiredMissingId = _db.MeetingParticipant(meetingId, missingUserId, isRequired: true);
        using (var ctx = _db.Context())
        {
            ctx.MeetingParticipants.Single(p => p.Id == requiredAttendedId).Attended = true;
            ctx.MeetingParticipants.Single(p => p.Id == requiredMissingId).Attended = false;
            ctx.SaveChanges();
        }

        using var genCtx = _db.Context();
        var result = await CreateService(genCtx).GenerateAsync(userId, meetingId);
        Assert.True(result.Success, result.Message);
        var brief = result.Data!;

        Assert.NotNull(brief.Source);
        Assert.Equal(BriefSourceKind.Draft, brief.Source!.Kind);
        Assert.NotEmpty(brief.Summary);

        Assert.Contains(brief.ResourceCommitments, r => r.Kind == ResourceCommitmentKind.NewService && r.Label.Contains("OT"));
        Assert.Contains(brief.ResourceCommitments, r => r.Kind == ResourceCommitmentKind.Placement);
        Assert.Contains(brief.ResourceCommitments, r => r.Kind == ResourceCommitmentKind.Esy);
        // The unchanged Speech row is never flagged as a commitment.
        Assert.DoesNotContain(brief.ResourceCommitments, r => r.Label.Contains("Speech"));

        var participantsItem = brief.Checklist.Single(c => c.Key == "requiredParticipants");
        Assert.Equal(false, participantsItem.Satisfied);

        var noticeItem = brief.Checklist.Single(c => c.Key == "noticeTiming");
        Assert.Null(noticeItem.Satisfied); // no MeetingScheduled notification on record

        var familyItem = brief.Checklist.Single(c => c.Key == "familyInput");
        Assert.Equal(false, familyItem.Satisfied);

        // GET reads the cache back verbatim (no recomputation).
        using var readCtx = _db.Context();
        var cached = await CreateService(readCtx).GetAsync(userId, meetingId);
        Assert.True(cached.Success, cached.Message);
        Assert.Equal(brief.Summary, cached.Data!.Summary);
    }

    [Fact]
    public async Task GenerateAsync_NoticeSentTenDaysAhead_ChecklistSatisfied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var studentId = _db.Student(schoolId);
        var (userId, _) = _db.Staff("lea2@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);

        var meetingStart = DateTime.UtcNow.AddDays(20);
        var meetingId = _db.Meeting(studentId, userId, meetingStart, status: MeetingStatus.Scheduled);

        using (var ctx = _db.Context())
        {
            ctx.Notifications.Add(new Notification
            {
                UserId = userId,
                Kind = NotificationKind.MeetingScheduled,
                Title = "Meeting scheduled",
                Body = "body",
                DedupKey = $"meeting-{meetingId}-0-MeetingScheduled",
                CreatedAt = meetingStart.AddDays(-15)
            });
            ctx.SaveChanges();
        }

        using var ctx2 = _db.Context();
        var result = await CreateService(ctx2).GenerateAsync(userId, meetingId);
        Assert.True(result.Success, result.Message);
        var noticeItem = result.Data!.Checklist.Single(c => c.Key == "noticeTiming");
        Assert.Equal(true, noticeItem.Satisfied);
    }

    [Fact]
    public async Task GetAsync_NoBriefGenerated_Fails()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var studentId = _db.Student(schoolId);
        var (userId, _) = _db.Staff("lea3@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Viewer);
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(1));

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetAsync(userId, meetingId);
        Assert.False(result.Success);
    }

    public void Dispose() => _db.Dispose();
}
