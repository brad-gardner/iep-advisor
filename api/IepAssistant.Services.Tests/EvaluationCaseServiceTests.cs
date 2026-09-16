using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 7 phase 2: evaluation case lifecycle (referral → consent → clock → determination → close),
/// the evaluation clock computed from consent (+60 calendar days), due-date override requiring a reason,
/// only one open case per student, evaluator-overdue notification dedup, and "Create IEP from ETR"
/// producing a prefilled Draft instance.
/// </summary>
public sealed class EvaluationCaseServiceTests : IDisposable
{
    private const int IepTypeId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public EvaluationCaseServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private sealed class NoopBlobStorageFake : IBlobStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake.blob/{blobPath}");
        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null)
            => Task.FromResult($"https://fake.blob/{blobPath}?sas=token");
    }

    /// <summary>Canned evidence bundle so <see cref="CreateIepFromEtrAsync"/> can be tested against the
    /// REAL <see cref="DocumentPrefillService"/> without wiring StudentEvidenceService's own dependencies.</summary>
    private sealed class FakeStudentEvidenceService : IStudentEvidenceService
    {
        public const string PresentLevelsText = "Reads at grade level per the evaluation team.";

        public Task<ServiceResult<StudentEvidenceBundle>> BuildForStaffAsync(int userId, int schoolStudentId, CancellationToken ct = default)
            => Task.FromResult(ServiceResult<StudentEvidenceBundle>.SuccessResult(new StudentEvidenceBundle
            {
                SchoolStudentId = schoolStudentId,
                Items = new List<EvidenceItem>
                {
                    new()
                    {
                        Id = "E1",
                        Kind = EvidenceKind.EtrFinding,
                        SourceType = "AuthoredDocumentVersion",
                        SourceId = 1,
                        SourceLabel = "ETR v1",
                        AuthorRole = "school",
                        Text = PresentLevelsText
                    }
                },
                Sources = new List<EvidenceSource>()
            }));
    }

    private IDocumentInstanceService CreateDocumentInstanceService(ApplicationDbContext ctx) => new DocumentInstanceService(
        ctx,
        new OrgAccessService(ctx),
        new TemplateResolutionService(ctx, NullLogger<TemplateResolutionService>.Instance),
        new TemplateAuthoringService(ctx, _audit, NullLogger<TemplateAuthoringService>.Instance),
        _audit,
        NullLogger<DocumentInstanceService>.Instance,
        new FakeStudentEvidenceService(),
        new DocumentPrefillService(ctx));

    private EvaluationCaseService CreateService(ApplicationDbContext ctx) => new(
        ctx,
        new OrgAccessService(ctx),
        new NoopBlobStorageFake(),
        new NotificationService(ctx),
        CreateDocumentInstanceService(ctx),
        NullLogger<EvaluationCaseService>.Instance);

    private sealed record Scenario(int StudentId, int LeadUserId, int EvaluatorUserId);

    private Scenario Seed(string prefix)
    {
        using var ctx = CreateContext();
        var lead = new User { Email = $"{prefix}-lead@example.com", PasswordHash = "x", FirstName = "Lee", LastName = "Case", Role = UserRole.Educator };
        var evaluator = new User { Email = $"{prefix}-eval@example.com", PasswordHash = "x", FirstName = "Evan", LastName = "Uator", Role = UserRole.Educator };
        ctx.Users.AddRange(lead, evaluator);
        ctx.SaveChanges();

        var district = new District { Name = prefix };
        ctx.Districts.Add(district);
        ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = prefix };
        ctx.Schools.Add(school);
        ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = lead.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        ctx.StaffProfiles.Add(new StaffProfile { UserId = evaluator.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan", LastName = "Ellis", CaseManagerUserId = lead.Id };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = lead.Id, Role = AccessRole.Collaborator, IsActive = true });
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = evaluator.Id, Role = AccessRole.Collaborator, IsActive = true });
        ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = student.Id, UserId = lead.Id, TeamRole = TeamRole.CaseManager, IsLead = true, IsActive = true });
        ctx.SaveChanges();

        return new Scenario(student.Id, lead.Id, evaluator.Id);
    }

    // ---------------------------------------------------------------- Lifecycle

    [Fact]
    public async Task CreateAsync_StartsOpen()
    {
        var s = Seed(nameof(CreateAsync_StartsOpen));
        using var ctx = CreateContext();

        var result = await CreateService(ctx).CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel
        {
            Kind = EvaluationCaseKind.Initial,
            ReferralDate = new DateTime(2026, 1, 5)
        });

        Assert.True(result.Success, result.Message);
        Assert.Equal(EvaluationCaseStatus.Open, result.Data!.Status);
    }

    [Fact]
    public async Task CreateAsync_SecondOpenCase_Fails()
    {
        var s = Seed(nameof(CreateAsync_SecondOpenCase_Fails));
        using var ctx = CreateContext();
        var service = CreateService(ctx);

        var first = await service.CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel { Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date });
        Assert.True(first.Success, first.Message);

        var second = await service.CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel { Kind = EvaluationCaseKind.Reevaluation, ReferralDate = DateTime.UtcNow.Date });

        Assert.False(second.Success);
    }

    [Fact]
    public async Task FullLifecycle_OpenThroughDetermined_TransitionsCorrectly()
    {
        var s = Seed(nameof(FullLifecycle_OpenThroughDetermined_TransitionsCorrectly));
        using var ctx = CreateContext();
        var service = CreateService(ctx);

        var created = await service.CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel { Kind = EvaluationCaseKind.Initial, ReferralDate = new DateTime(2026, 1, 1) });
        Assert.Equal(EvaluationCaseStatus.Open, created.Data!.Status);

        var requested = await service.RequestConsentAsync(s.LeadUserId, s.StudentId, new DateTime(2026, 1, 3));
        Assert.Equal(EvaluationCaseStatus.ConsentPending, requested.Data!.Status);

        var received = await service.ReceiveConsentAsync(s.LeadUserId, s.StudentId, new ReceiveConsentModel { ReceivedAt = new DateTime(2026, 1, 10) });
        Assert.Equal(EvaluationCaseStatus.InProgress, received.Data!.Status);
        Assert.Equal(new DateTime(2026, 3, 11), received.Data.DeterminationDueDate); // +60 calendar days

        var determined = await service.DetermineAsync(s.LeadUserId, s.StudentId, new DetermineEvaluationModel
        {
            Outcome = EligibilityOutcome.Eligible,
            DeterminationDate = new DateTime(2026, 3, 1),
            Rationale = "Meets criteria under OHI."
        });
        Assert.Equal(EvaluationCaseStatus.Determined, determined.Data!.Status);

        var closed = await service.CloseAsync(s.LeadUserId, s.StudentId);
        Assert.Equal(EvaluationCaseStatus.Closed, closed.Data!.Status);
    }

    [Fact]
    public async Task DetermineAsync_NotEligible_ClosesImmediately()
    {
        var s = Seed(nameof(DetermineAsync_NotEligible_ClosesImmediately));
        using var ctx = CreateContext();
        var service = CreateService(ctx);

        await service.CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel { Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date });
        await service.ReceiveConsentAsync(s.LeadUserId, s.StudentId, new ReceiveConsentModel { ReceivedAt = DateTime.UtcNow.Date });

        var determined = await service.DetermineAsync(s.LeadUserId, s.StudentId, new DetermineEvaluationModel
        {
            Outcome = EligibilityOutcome.NotEligible,
            DeterminationDate = DateTime.UtcNow.Date,
            Rationale = "Does not meet eligibility criteria."
        });

        Assert.True(determined.Success, determined.Message);
        Assert.Equal(EvaluationCaseStatus.Closed, determined.Data!.Status);
        Assert.NotNull(determined.Data.ClosedAt);
    }

    [Fact]
    public async Task OverrideDueDateAsync_WithoutReason_Fails()
    {
        var s = Seed(nameof(OverrideDueDateAsync_WithoutReason_Fails));
        using var ctx = CreateContext();
        var service = CreateService(ctx);
        await service.CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel { Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date });

        var result = await service.OverrideDueDateAsync(s.LeadUserId, s.StudentId, new OverrideDueDateModel
        {
            DeterminationDueDate = DateTime.UtcNow.Date.AddDays(90),
            Reason = ""
        });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task OverrideDueDateAsync_WithReason_Succeeds()
    {
        var s = Seed(nameof(OverrideDueDateAsync_WithReason_Succeeds));
        using var ctx = CreateContext();
        var service = CreateService(ctx);
        await service.CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel { Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date });

        var newDue = DateTime.UtcNow.Date.AddDays(90);
        var result = await service.OverrideDueDateAsync(s.LeadUserId, s.StudentId, new OverrideDueDateModel
        {
            DeterminationDueDate = newDue,
            Reason = "Team requested a extension due to a delayed evaluator report."
        });

        Assert.True(result.Success, result.Message);
        Assert.Equal(newDue, result.Data!.DeterminationDueDate);
        Assert.Equal("Team requested a extension due to a delayed evaluator report.", result.Data.DueDateOverrideReason);
    }

    // ---------------------------------------------------------------- Evaluator overdue notifications

    [Fact]
    public async Task RunOverdueNotificationsAsync_SamePassTwice_DedupsPerAssignmentPerDay()
    {
        var s = Seed(nameof(RunOverdueNotificationsAsync_SamePassTwice_DedupsPerAssignmentPerDay));
        int caseId;
        using (var ctx = CreateContext())
        {
            var service = CreateService(ctx);
            var created = await service.CreateAsync(s.LeadUserId, s.StudentId, new CreateEvaluationCaseModel { Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date.AddDays(-30) });
            caseId = created.Data!.Id;
            var assignment = await service.AddAssignmentAsync(s.LeadUserId, s.StudentId, new CreateEvaluatorAssignmentModel
            {
                UserId = s.EvaluatorUserId,
                Domain = "Speech-Language",
                DueDate = DateTime.UtcNow.Date.AddDays(-5) // already overdue, never submitted
            });
            Assert.True(assignment.Success, assignment.Message);
        }

        using (var ctx = CreateContext())
        {
            await CreateService(ctx).RunOverdueNotificationsAsync();
        }
        using (var ctx = CreateContext())
        {
            await CreateService(ctx).RunOverdueNotificationsAsync();
        }

        using var readCtx = CreateContext();
        var count = await readCtx.Notifications.CountAsync(n => n.Kind == NotificationKind.EvaluatorOverdue && n.UserId == s.EvaluatorUserId);
        Assert.Equal(1, count); // the second pass on the same day is a no-op (NotifyAsync's rolling 24h dedup)
    }

    // ---------------------------------------------------------------- ETR handoff

    [Fact]
    public async Task CreateIepFromEtrAsync_ProducesDraftInstanceWithPrefilledValues()
    {
        var s = Seed(nameof(CreateIepFromEtrAsync_ProducesDraftInstanceWithPrefilledValues));

        Guid plaafpKey;
        using (var ctx = CreateContext())
        {
            plaafpKey = Guid.NewGuid();
            var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
            ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { version } });
            ctx.SaveChanges();
            ctx.TemplateSections.Add(new TemplateSection
            {
                DocumentTemplateVersionId = version.Id,
                SectionKey = Guid.NewGuid(),
                Title = "Present Levels",
                DisplayOrder = 0,
                Fields =
                {
                    new TemplateField
                    {
                        DocumentTemplateVersionId = version.Id,
                        FieldKey = plaafpKey,
                        FieldType = FieldType.RichText,
                        Label = "Present Levels",
                        DisplayOrder = 0,
                        ConfigJson = TemplateGraphBuilder.RichTextConfig(FieldSemantics.PresentLevels)
                    }
                }
            });
            ctx.SaveChanges();
        }

        int instanceId;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).CreateIepFromEtrAsync(s.LeadUserId, s.StudentId);
            Assert.True(result.Success, result.Message);
            instanceId = result.Data;
        }

        using var readCtx = CreateContext();
        var instance = await readCtx.DocumentInstances.SingleAsync(i => i.Id == instanceId);
        Assert.Equal(DocumentInstanceStatus.Draft, instance.Status);
        var values = JsonNode.Parse(instance.ValuesJson) as JsonObject;
        var plaafpValue = values?[plaafpKey.ToString()]?.ToString();
        Assert.NotNull(plaafpValue);
        Assert.Contains(FakeStudentEvidenceService.PresentLevelsText, plaafpValue);
    }

    public void Dispose() => _connection.Dispose();
}
