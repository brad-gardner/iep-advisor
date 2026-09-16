using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 5, deliverable A: <see cref="HomeService"/> role dispatch and scoping. Staff-tier callers see only
/// their own meetings/obligations/drafts (never a colleague's); admin callers see scope-wide rows sorted
/// by student, never by staff; parent/student homes are meeting-relative and degrade gracefully with no
/// links. <see cref="HomeService_QueryBounds"/> guards the "~8 queries per role" budget.
/// </summary>
public sealed class HomeServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private HomeService CreateService(Domain.Data.ApplicationDbContext ctx)
    {
        var orgAccess = new OrgAccessService(ctx);
        var obligationService = new ObligationService(ctx, orgAccess);
        var completeness = new DocumentCompletenessService(ctx, new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance));
        return new HomeService(ctx, orgAccess, obligationService, completeness);
    }

    /// <summary>Seeds a one-required-field Published template version and returns its id.</summary>
    private int SeedTemplateVersion(Domain.Data.ApplicationDbContext ctx, int docTypeId, Guid fieldKey)
    {
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        var template = new DocumentTemplate { DocumentTypeId = docTypeId, Name = "T", Versions = { version } };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();
        ctx.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = version.Id,
            SectionKey = Guid.NewGuid(),
            Title = "Section",
            DisplayOrder = 0,
            Fields = { new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = fieldKey, FieldType = FieldType.Text, Label = "Name", Required = true, DisplayOrder = 0 } }
        });
        ctx.SaveChanges();
        return version.Id;
    }

    private int SeedDraftInstance(Domain.Data.ApplicationDbContext ctx, int studentId, int templateVersionId, int docTypeId, int lastEditedByUserId)
    {
        var instance = new DocumentInstance
        {
            SchoolStudentId = studentId,
            DocumentTypeId = docTypeId,
            DocumentTemplateVersionId = templateVersionId,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = "{}",
            RowVersion = Guid.NewGuid().ToByteArray(),
            LastEditedByUserId = lastEditedByUserId,
            LastEditedAt = DateTime.UtcNow
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();
        return instance.Id;
    }

    // ----------------------------------------------------------------- Staff home scoping

    [Fact]
    public async Task StaffHome_CaseManager_SeesOnlyOwnMeetingsObligationsAndDrafts()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userA, _) = _db.Staff("teacherA@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Ana", last: "Alpha");
        var (userB, _) = _db.Staff("teacherB@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Bea", last: "Beta");

        var studentA = _db.Student(schoolId, "Sam", "A");
        var studentB = _db.Student(schoolId, "Sky", "B");
        _db.TeamMember(studentA, userA, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(studentB, userB, TeamRole.CaseManager, isLead: true);

        // Overdue annual review for both, so both would show up in "obligations" if scoping leaked.
        using (var seedCtx = _db.Context())
        {
            seedCtx.SchoolStudents.Single(s => s.Id == studentA).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-5);
            seedCtx.SchoolStudents.Single(s => s.Id == studentB).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-5);
            seedCtx.SaveChanges();
        }

        var meetingA = _db.Meeting(studentA, userA, DateTime.UtcNow.AddHours(1));
        _db.MeetingParticipant(meetingA, userA);
        var meetingB = _db.Meeting(studentB, userB, DateTime.UtcNow.AddHours(2));
        _db.MeetingParticipant(meetingB, userB);

        var fieldKey = Guid.NewGuid();
        int templateVersionId;
        int draftA;
        int draftB;
        using (var seedCtx = _db.Context())
        {
            templateVersionId = SeedTemplateVersion(seedCtx, docTypeId: 1, fieldKey);
        }
        using (var seedCtx = _db.Context())
        {
            draftA = SeedDraftInstance(seedCtx, studentA, templateVersionId, docTypeId: 1, lastEditedByUserId: userA);
            draftB = SeedDraftInstance(seedCtx, studentB, templateVersionId, docTypeId: 1, lastEditedByUserId: userB);
        }

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(userA);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Staff!;
        Assert.Equal(StaffHomeVariant.CaseManager, home.Variant);

        Assert.Single(home.MeetingsThisWeek);
        Assert.Equal(meetingA, home.MeetingsThisWeek[0].Id);

        Assert.Single(home.Obligations);
        Assert.Equal(studentA, home.Obligations[0].SchoolStudentId);

        Assert.Single(home.Drafts);
        Assert.Equal(draftA, home.Drafts[0].InstanceId);

        // Admin-only sections stay unset for a non-admin variant.
        Assert.Null(home.RosterAttention);
        Assert.Null(home.OverdueByCaseManager);
        Assert.Null(home.ComplianceSummary);
    }

    [Fact]
    public async Task StaffHome_ProviderVariant_MapsFromRelatedServiceProviderRole()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("provider@example.com", districtId, schoolId, Models.OrgRoleIds.RelatedServiceProvider);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(userId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(StaffHomeVariant.Provider, result.Data!.Staff!.Variant);
        // Plan-6/7 sections are always empty-safe, regardless of variant.
        Assert.Empty(result.Data.Staff.SharedDraftsAwaitingFamily);
        Assert.Empty(result.Data.Staff.FamilyResponsesToReview);
        Assert.Empty(result.Data.Staff.ProviderRequestsIOwe);
    }

    // ----------------------------------------------------------------- Admin home: sorted by student, not staff

    [Fact]
    public async Task StaffHome_DistrictAdmin_OverdueByCaseManagerSortedByStudentName_NotStaffName()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (adminId, _) = _db.Staff("admin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);

        // Sorted by STUDENT name: "Amy Zephyr" < "Zach Adams". Sorted by CASE MANAGER name it would be
        // reversed ("Amy Adams" < "Zach Young"), which the row order must NOT follow.
        var (leadForZephyr, _) = _db.Staff("lead1@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Zach", last: "Young");
        var (leadForAdams, _) = _db.Staff("lead2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Amy", last: "Adams");

        var studentZephyr = _db.Student(schoolId, "Amy", "Zephyr");
        var studentAdams = _db.Student(schoolId, "Zach", "Adams");
        _db.TeamMember(studentZephyr, leadForZephyr, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(studentAdams, leadForAdams, TeamRole.CaseManager, isLead: true);

        using (var seedCtx = _db.Context())
        {
            seedCtx.SchoolStudents.Single(s => s.Id == studentZephyr).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-3);
            seedCtx.SchoolStudents.Single(s => s.Id == studentAdams).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-3);
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(adminId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Staff!;
        Assert.Equal(StaffHomeVariant.DistrictAdmin, home.Variant);

        var rows = home.OverdueByCaseManager!.Where(r => r.StudentId == studentZephyr || r.StudentId == studentAdams).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Amy Zephyr", rows[0].StudentName);
        Assert.Equal("Zach Adams", rows[1].StudentName);

        // District-wide obligations are empty here for admins (the board/roster attention covers it).
        Assert.Empty(home.Obligations);
        Assert.NotNull(home.RosterAttention);
        Assert.NotNull(home.ComplianceSummary);
        Assert.True(home.ComplianceSummary!.OverdueAnnual >= 2);
    }

    [Fact]
    public async Task StaffHome_SchoolAdmin_ComplianceSummaryStaysNull()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (adminId, _) = _db.Staff("schooladmin@example.com", districtId, schoolId, Models.OrgRoleIds.SchoolAdmin);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(adminId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Staff!;
        Assert.Equal(StaffHomeVariant.SchoolAdmin, home.Variant);
        Assert.NotNull(home.RosterAttention);
        Assert.Null(home.ComplianceSummary); // DistrictAdmin only
    }

    // ----------------------------------------------------------------- Parent home

    [Fact]
    public async Task ParentHome_TwoChildren_NextMeetingPicksEarliest_DocumentsFromBoth()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var parentUserId = _db.SeedUser("parent@example.com", UserRole.Parent, "Pat", "Parent");

        var child1 = _db.ChildProfile(parentUserId, "Kid", "One");
        var child2 = _db.ChildProfile(parentUserId, "Kid", "Two");
        var student1 = _db.Student(schoolId, "Kid", "One");
        var student2 = _db.Student(schoolId, "Kid", "Two");
        _db.ChildLink(student1, child1);
        _db.ChildLink(student2, child2);

        var (creatorUserId, _) = _db.Staff("creator@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var laterMeeting = _db.Meeting(student1, creatorUserId, DateTime.UtcNow.AddDays(10));
        var earlierMeeting = _db.Meeting(student2, creatorUserId, DateTime.UtcNow.AddDays(2));

        int templateVersionId;
        using (var seedCtx = _db.Context())
        {
            templateVersionId = SeedTemplateVersion(seedCtx, docTypeId: 1, Guid.NewGuid());
        }
        using (var seedCtx = _db.Context())
        {
            seedCtx.AuthoredDocumentVersions.Add(new AuthoredDocumentVersion
            {
                SchoolStudentId = student1, DocumentTypeId = 1, DocumentTemplateVersionId = templateVersionId, VersionNumber = 1,
                ValuesJson = "{}", FinalizedByUserId = creatorUserId, FinalizedAt = DateTime.UtcNow.AddDays(-2)
            });
            seedCtx.AuthoredDocumentVersions.Add(new AuthoredDocumentVersion
            {
                SchoolStudentId = student2, DocumentTypeId = 1, DocumentTemplateVersionId = templateVersionId, VersionNumber = 1,
                ValuesJson = "{}", FinalizedByUserId = creatorUserId, FinalizedAt = DateTime.UtcNow.AddDays(-1)
            });
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(parentUserId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Parent!;
        Assert.Equal(2, home.Children.Count);
        Assert.All(home.Children, c => Assert.True(c.HasSchoolLink));

        Assert.NotNull(home.NextMeeting);
        Assert.Equal(earlierMeeting, home.NextMeeting!.Id);
        Assert.Equal(child2, home.NextMeeting.ChildId);
        Assert.True(home.NextMeeting.DaysUntil <= 2);

        Assert.Equal(2, home.DocumentsToReview.Count);
        Assert.Contains(home.DocumentsToReview, d => d.ChildId == child1);
        Assert.Contains(home.DocumentsToReview, d => d.ChildId == child2);
        Assert.Empty(home.SetupNotices);
    }

    [Fact]
    public async Task ParentHome_ChildWithNoSchoolLink_EmptyListsPlusNotice()
    {
        var parentUserId = _db.SeedUser("parent2@example.com", UserRole.Parent, "Pat", "Parent");
        _db.ChildProfile(parentUserId, "Lonely", "Kid");

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(parentUserId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Parent!;
        Assert.Single(home.Children);
        Assert.False(home.Children[0].HasSchoolLink);
        Assert.Null(home.NextMeeting);
        Assert.Empty(home.DocumentsToReview);
        Assert.NotEmpty(home.SetupNotices);
    }

    [Fact]
    public async Task ParentHome_NoChildrenAtAll_EmptyListsPlusNotice()
    {
        var parentUserId = _db.SeedUser("parent3@example.com", UserRole.Parent, "Pat", "Parent");

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(parentUserId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Parent!;
        Assert.Empty(home.Children);
        Assert.Null(home.NextMeeting);
        Assert.Empty(home.DocumentsToReview);
        Assert.Single(home.SetupNotices);
    }

    // ----------------------------------------------------------------- Student home

    [Fact]
    public async Task StudentHome_LinkedStudent_ShowsNextMeetingAndNudgeUntilWorkspaceHasEntries()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentUserId = _db.SeedUser("student@example.com", UserRole.Student, "Stu", "Dent");
        var studentId = _db.Student(schoolId, "Stu", "Dent");
        _db.StudentProfile(studentId, studentUserId);

        var (creatorUserId, _) = _db.Staff("creator2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var meetingId = _db.Meeting(studentId, creatorUserId, DateTime.UtcNow.AddDays(3));

        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).GetForUserAsync(studentUserId);
            Assert.True(result.Success, result.Message);
            var home = result.Data!.Student!;
            Assert.Equal(studentId, home.LinkedStudentId);
            Assert.NotNull(home.NextMeeting);
            Assert.Equal(meetingId, home.NextMeeting!.Id);
            Assert.NotNull(home.WorkspaceNudge);
        }

        using (var ctx = _db.Context())
        {
            var workspace = new StudentWorkspace { UserId = studentUserId };
            ctx.Set<StudentWorkspace>().Add(workspace);
            ctx.SaveChanges();
            ctx.Set<StudentWorkspaceEntry>().Add(new StudentWorkspaceEntry { StudentWorkspaceId = workspace.Id, EntryKind = StudentEntryKind.Strength, Content = "I persevere." });
            ctx.SaveChanges();
        }

        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).GetForUserAsync(studentUserId);
            Assert.Null(result.Data!.Student!.WorkspaceNudge);
        }
    }

    [Fact]
    public async Task StudentHome_NoLinkedProfile_NextMeetingAndLinkedIdAreNull()
    {
        var studentUserId = _db.SeedUser("unlinked-student@example.com", UserRole.Student, "No", "Link");

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(studentUserId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Student!;
        Assert.Null(home.LinkedStudentId);
        Assert.Null(home.NextMeeting);
    }

    // ----------------------------------------------------------------- Query-count bounds

    [Fact]
    public async Task HomeService_QueryBounds()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (adminId, _) = _db.Staff("boundsadmin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        var (leadId, _) = _db.Staff("boundslead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var studentId = _db.Student(schoolId, "Bud", "Get");
        _db.TeamMember(studentId, leadId, TeamRole.CaseManager, isLead: true);
        using (var seedCtx = _db.Context())
        {
            seedCtx.SchoolStudents.Single(s => s.Id == studentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1);
            seedCtx.SaveChanges();
        }
        var meetingId = _db.Meeting(studentId, leadId, DateTime.UtcNow.AddHours(3));
        _db.MeetingParticipant(meetingId, leadId);

        // Admin path: profile/scope label, meetings-this-week, drafts, obligations-for-scope, noLead,
        // noFamily (no per-draft/per-bucket N+1) — measured at 7; bounded well under the ~8 target.
        var counter = new DbActivityCounter();
        using (var ctx = _db.Context(counter))
        {
            var result = await CreateService(ctx).GetForUserAsync(adminId);
            Assert.True(result.Success, result.Message);
        }
        Assert.True(counter.Queries <= 9, $"DistrictAdmin home issued {counter.Queries} queries");

        // Staff-tier path: profile/scope label, meetings-this-week, lead-only obligations, drafts —
        // measured at 5.
        counter.Reset();
        using (var ctx = _db.Context(counter))
        {
            var result = await CreateService(ctx).GetForUserAsync(leadId);
            Assert.True(result.Success, result.Message);
        }
        Assert.True(counter.Queries <= 7, $"CaseManager home issued {counter.Queries} queries");

        // Parent and student paths are lighter still (no obligation/roster computation at all).
        var parentUserId = _db.SeedUser("boundsparent@example.com", UserRole.Parent, "Pat", "Parent");
        var childId = _db.ChildProfile(parentUserId, "Kid", "Bounds");
        _db.ChildLink(studentId, childId);

        counter.Reset();
        using (var ctx = _db.Context(counter))
        {
            var result = await CreateService(ctx).GetForUserAsync(parentUserId);
            Assert.True(result.Success, result.Message);
        }
        Assert.True(counter.Queries <= 6, $"Parent home issued {counter.Queries} queries");

        var studentUserId = _db.SeedUser("boundsstudent@example.com", UserRole.Student, "Stu", "Dent");
        _db.StudentProfile(studentId, studentUserId);

        counter.Reset();
        using (var ctx = _db.Context(counter))
        {
            var result = await CreateService(ctx).GetForUserAsync(studentUserId);
            Assert.True(result.Success, result.Message);
        }
        Assert.True(counter.Queries <= 6, $"Student home issued {counter.Queries} queries");
    }

    public void Dispose() => _db.Dispose();
}
