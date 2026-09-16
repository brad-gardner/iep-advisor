using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 7, decision 7: offline family contact attempts and input are recordable (Collaborator+),
/// readable (Viewer+), and offline input surfaces in the student evidence bundle with the contracted
/// kind/label so it grounds AI assist and the meeting brief the same way any other evidence source does.
/// </summary>
public sealed class FamilyContactServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private FamilyContactService CreateService(ApplicationDbContext ctx) => new(ctx, new OrgAccessService(ctx));

    private sealed class NoClaude : IClaudeClient
    {
        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private StudentEvidenceService CreateEvidenceService(ApplicationDbContext ctx)
    {
        var access = new AccessService(ctx);
        var org = new OrgAccessService(ctx);
        var workspace = new StudentWorkspaceService(ctx, access, org, new NoClaude(), NullLogger<StudentWorkspaceService>.Instance);
        var contributions = new ParentContributionService(ctx, access, org, new CapturingAuditLogger());
        return new StudentEvidenceService(ctx, org, workspace, contributions, new CapturingAuditLogger());
    }

    [Fact]
    public async Task RecordContactAttempt_Persists_AndListedNewestFirst()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId);
        var (userId, _) = _db.Staff("cm@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);

        using (var ctx = _db.Context())
        {
            var service = CreateService(ctx);
            var first = await service.RecordContactAttemptAsync(userId, studentId, new CreateFamilyContactAttemptModel
            {
                AttemptedAt = DateTime.UtcNow.AddDays(-2),
                Method = FamilyContactMethod.Phone,
                Outcome = FamilyContactOutcome.NoAnswer
            });
            Assert.True(first.Success, first.Message);

            var second = await service.RecordContactAttemptAsync(userId, studentId, new CreateFamilyContactAttemptModel
            {
                AttemptedAt = DateTime.UtcNow.AddDays(-1),
                Method = FamilyContactMethod.Email,
                Outcome = FamilyContactOutcome.Reached,
                Note = "Discussed upcoming IEP meeting"
            });
            Assert.True(second.Success, second.Message);
        }

        using var readCtx = _db.Context();
        var list = await CreateService(readCtx).GetContactAttemptsAsync(userId, studentId);
        Assert.True(list.Success, list.Message);
        Assert.Equal(2, list.Data!.Count);
        Assert.Equal(FamilyContactMethod.Email, list.Data![0].Method); // newest AttemptedAt first
        Assert.Equal(FamilyContactMethod.Phone, list.Data![1].Method);
    }

    [Fact]
    public async Task RecordOfflineInput_Persists_AndAppearsInStudentEvidenceBundle()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (userId, _) = _db.Staff("cm2@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);

        using (var ctx = _db.Context())
        {
            var service = CreateService(ctx);
            var result = await service.RecordOfflineInputAsync(userId, studentId, new CreateOfflineFamilyInputModel
            {
                ReceivedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Method = FamilyContactMethod.InPerson,
                Summary = "Parent stopped by to discuss progress; supportive of proposed goals."
            });
            Assert.True(result.Success, result.Message);
        }

        using (var readCtx = _db.Context())
        {
            var list = await CreateService(readCtx).GetOfflineInputAsync(userId, studentId);
            Assert.True(list.Success, list.Message);
            Assert.Single(list.Data!);
        }

        using var evidenceCtx = _db.Context();
        var bundle = await CreateEvidenceService(evidenceCtx).BuildForStaffAsync(userId, studentId);
        Assert.True(bundle.Success, bundle.Message);
        var item = Assert.Single(bundle.Data!.Items, i => i.Kind == EvidenceKind.OfflineFamilyInput);
        Assert.Equal("Family input (offline, 2026-06-01)", item.SourceLabel);
        Assert.Contains("supportive of proposed goals", item.Text);
        Assert.Equal("family", item.AuthorRole);
    }

    [Fact]
    public async Task NonCollaborator_CannotRecordContactAttempt_ButCanRead()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId);
        var (viewerUserId, _) = _db.Staff("viewer@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, viewerUserId, AccessRole.Viewer);

        using var ctx = _db.Context();
        var service = CreateService(ctx);

        var write = await service.RecordContactAttemptAsync(viewerUserId, studentId, new CreateFamilyContactAttemptModel
        {
            Method = FamilyContactMethod.Phone,
            Outcome = FamilyContactOutcome.LeftMessage
        });
        Assert.False(write.Success);

        var read = await service.GetContactAttemptsAsync(viewerUserId, studentId);
        Assert.True(read.Success);
    }

    public void Dispose() => _db.Dispose();
}
