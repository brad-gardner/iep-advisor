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
        // Shares the same IOrgAccessService instance HomeService uses (matches production DI, where
        // IOrgAccessService is Scoped) so its per-request staff-context memo is actually shared across
        // HomeService/ObligationService/DistrictService, as it would be for a real request.
        var districtService = new DistrictService(ctx, orgAccess, NullLogger<DistrictService>.Instance);
        return new HomeService(ctx, orgAccess, obligationService, completeness, districtService);
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

    /// <summary>Plan 6: seeds an Active (by default) SharedDraftRevision directly, bypassing DraftSharingService.</summary>
    private int SeedSharedDraftRevision(Domain.Data.ApplicationDbContext ctx, int instanceId, int templateVersionId, int sharedByUserId, SharedDraftStatus status = SharedDraftStatus.Active, int revisionNumber = 1)
    {
        var revision = new SharedDraftRevision
        {
            DocumentInstanceId = instanceId,
            RevisionNumber = revisionNumber,
            ValuesJson = "{}",
            DocumentTemplateVersionId = templateVersionId,
            SharedByUserId = sharedByUserId,
            SharedAt = DateTime.UtcNow,
            Status = status
        };
        ctx.SharedDraftRevisions.Add(revision);
        ctx.SaveChanges();
        return revision.Id;
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

    // ----------------------------------------------------------------- Drafts: current access required (todos/080)

    [Fact]
    public async Task StaffHome_Drafts_RemovedTeamMemberNoLongerSeesDraft()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("removed-lead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var studentId = _db.Student(schoolId, "Sam", "Removed");
        _db.TeamMember(studentId, userId, TeamRole.CaseManager, isLead: true);

        var fieldKey = Guid.NewGuid();
        int templateVersionId;
        int draftId;
        using (var seedCtx = _db.Context())
        {
            templateVersionId = SeedTemplateVersion(seedCtx, docTypeId: 1, fieldKey);
        }
        using (var seedCtx = _db.Context())
        {
            draftId = SeedDraftInstance(seedCtx, studentId, templateVersionId, docTypeId: 1, lastEditedByUserId: userId);
        }

        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).GetForUserAsync(userId);
            Assert.True(result.Success, result.Message);
            Assert.Contains(result.Data!.Staff!.Drafts, d => d.InstanceId == draftId);
        }

        // Mirrors StudentTeamWriter.DeactivateMemberAsync: both the team membership AND its access row go inactive.
        using (var seedCtx = _db.Context())
        {
            var member = seedCtx.StudentTeamMembers.Single(m => m.SchoolStudentId == studentId && m.UserId == userId);
            member.IsActive = false;
            member.IsLead = false;
            var access = seedCtx.SchoolStudentAccesses.Single(a => a.SchoolStudentId == studentId && a.UserId == userId);
            access.IsActive = false;
            seedCtx.SchoolStudents.Single(s => s.Id == studentId).CaseManagerUserId = null;
            seedCtx.SaveChanges();
        }

        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).GetForUserAsync(userId);
            Assert.True(result.Success, result.Message);
            // "I last edited it" is no longer a visibility grant on its own once current access is gone.
            Assert.DoesNotContain(result.Data!.Staff!.Drafts, d => d.InstanceId == draftId);
        }
    }

    [Fact]
    public async Task StaffHome_Drafts_TransferredStudent_NonPortableTeacherNoLongerSeesDraft()
    {
        var districtId = _db.District();
        var schoolA = _db.School(districtId, "School A");
        var schoolB = _db.School(districtId, "School B");
        var (teacherId, _) = _db.Staff("teacher-transfer@example.com", districtId, schoolA, Models.OrgRoleIds.Teacher);
        var (adminId, _) = _db.Staff("transfer-admin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        var studentId = _db.Student(schoolA, "Terry", "Transfer");
        _db.TeamMember(studentId, teacherId, TeamRole.CaseManager, isLead: true);

        var fieldKey = Guid.NewGuid();
        int templateVersionId;
        int draftId;
        using (var seedCtx = _db.Context())
        {
            templateVersionId = SeedTemplateVersion(seedCtx, docTypeId: 1, fieldKey);
        }
        using (var seedCtx = _db.Context())
        {
            draftId = SeedDraftInstance(seedCtx, studentId, templateVersionId, docTypeId: 1, lastEditedByUserId: teacherId);
        }

        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).GetForUserAsync(teacherId);
            Assert.True(result.Success, result.Message);
            Assert.Contains(result.Data!.Staff!.Drafts, d => d.InstanceId == draftId);
        }

        // The student transfers to a different school; the teacher (bound to School A, not portable) is
        // deactivated off the team and loses their access row (EducatorService.TransferStudentAsync ->
        // StudentTeamBatch.DeactivateNonPortable).
        using (var ctx = _db.Context())
        {
            var orgAccess = new OrgAccessService(ctx);
            var educatorService = new EducatorService(ctx, orgAccess, new CapturingAuditLogger(), NullLogger<EducatorService>.Instance);
            var transferResult = await educatorService.TransferStudentAsync(adminId, studentId, schoolB);
            Assert.True(transferResult.Success, transferResult.Message);
        }

        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).GetForUserAsync(teacherId);
            Assert.True(result.Success, result.Message);
            Assert.DoesNotContain(result.Data!.Staff!.Drafts, d => d.InstanceId == draftId);
        }
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

    [Fact]
    public async Task StaffHome_PopulatesSharedDraftsAwaitingFamily_AndFamilyResponsesToReview()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (teacherId, _) = _db.Staff("sharer@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var studentAwaiting = _db.Student(schoolId, "Awaiting", "Family");
        var studentResponded = _db.Student(schoolId, "Responded", "Family");
        _db.TeamMember(studentAwaiting, teacherId, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(studentResponded, teacherId, TeamRole.CaseManager, isLead: true);

        var fieldKey = Guid.NewGuid();
        int templateVersionId;
        int awaitingInstanceId, respondedInstanceId;
        using (var seedCtx = _db.Context())
        {
            templateVersionId = SeedTemplateVersion(seedCtx, docTypeId: 1, fieldKey);
        }
        using (var seedCtx = _db.Context())
        {
            awaitingInstanceId = SeedDraftInstance(seedCtx, studentAwaiting, templateVersionId, docTypeId: 1, lastEditedByUserId: teacherId);
            respondedInstanceId = SeedDraftInstance(seedCtx, studentResponded, templateVersionId, docTypeId: 1, lastEditedByUserId: teacherId);
        }

        int respondedRevisionId;
        using (var seedCtx = _db.Context())
        {
            SeedSharedDraftRevision(seedCtx, awaitingInstanceId, templateVersionId, teacherId);
            respondedRevisionId = SeedSharedDraftRevision(seedCtx, respondedInstanceId, templateVersionId, teacherId);
        }

        var parentUserId = _db.SeedUser("familyresp@example.com", UserRole.Parent, "Res", "Ponder");
        using (var seedCtx = _db.Context())
        {
            seedCtx.Set<DraftResponse>().Add(new DraftResponse
            {
                SharedDraftRevisionId = respondedRevisionId,
                ParentUserId = parentUserId,
                Kind = DraftResponseKind.Question,
                Text = "Why 30 minutes?",
                Status = DraftResponseStatus.Open
            });
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(teacherId);
        Assert.True(result.Success, result.Message);

        Assert.Contains(result.Data!.Staff!.SharedDraftsAwaitingFamily, d => d.InstanceId == awaitingInstanceId);
        Assert.DoesNotContain(result.Data.Staff.SharedDraftsAwaitingFamily, d => d.InstanceId == respondedInstanceId);

        Assert.Contains(result.Data.Staff.FamilyResponsesToReview, d => d.InstanceId == respondedInstanceId);
        Assert.DoesNotContain(result.Data.Staff.FamilyResponsesToReview, d => d.InstanceId == awaitingInstanceId);
    }

    [Fact]
    public async Task ParentHome_DocumentsToReview_IncludesUnacknowledgedActiveSharedDraft()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (teacherId, _) = _db.Staff("teacher@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var studentId = _db.Student(schoolId, "Jordan", "Ellis");
        var parentUserId = _db.SeedUser("parentdocs@example.com", UserRole.Parent, "Dana", "Parent");
        var childId = _db.ChildProfile(parentUserId, "Jordan", "Ellis");
        _db.ChildLink(studentId, childId);

        var fieldKey = Guid.NewGuid();
        int templateVersionId;
        int instanceId;
        using (var seedCtx = _db.Context())
        {
            templateVersionId = SeedTemplateVersion(seedCtx, docTypeId: 1, fieldKey);
        }
        using (var seedCtx = _db.Context())
        {
            instanceId = SeedDraftInstance(seedCtx, studentId, templateVersionId, docTypeId: 1, lastEditedByUserId: teacherId);
        }
        int revisionId;
        using (var seedCtx = _db.Context())
        {
            // Revision number 7 on a fresh instance so the number and the id cannot coincide by accident.
            revisionId = SeedSharedDraftRevision(seedCtx, instanceId, templateVersionId, teacherId, revisionNumber: 7);
        }

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(parentUserId);
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Data!.Parent!.DocumentsToReview, d => d.Kind == ParentDocumentKind.SharedDraft && d.ChildId == childId);
        Assert.Equal(revisionId, item.Id);
        Assert.Equal(7, item.VersionNumber);
        // The route resolves the revision by id — never by the per-document revision number.
        Assert.Equal($"/children/{childId}/shared-drafts/{revisionId}", item.LinkPath);
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

    /// <summary>Review-fix contract, todos/078: the DistrictAdmin home's ComplianceSummary must be the
    /// SAME numbers as the no-filter compliance board for the same district — not an independently
    /// re-derived aggregation that could drift from it.</summary>
    [Fact]
    public async Task StaffHome_DistrictAdmin_ComplianceSummaryMatchesComplianceBoardSummary()
    {
        var districtId = _db.District();
        var schoolA = _db.School(districtId, "School A");
        var schoolB = _db.School(districtId, "School B");
        var (adminId, _) = _db.Staff("parity-admin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        var (leadId, _) = _db.Staff("parity-lead@example.com", districtId, schoolA, Models.OrgRoleIds.Teacher);

        var overdueStudent = _db.Student(schoolA, "Overdue", "Annual");
        _db.TeamMember(overdueStudent, leadId, TeamRole.CaseManager, isLead: true);
        var due30Student = _db.Student(schoolB, "Due", "Soon");
        var noLeadStudent = _db.Student(schoolB, "No", "Lead");

        using (var seedCtx = _db.Context())
        {
            var today = DateTime.UtcNow.Date;
            seedCtx.SchoolStudents.Single(s => s.Id == overdueStudent).AnnualReviewDueDate = today.AddDays(-5);
            seedCtx.SchoolStudents.Single(s => s.Id == overdueStudent).ReevaluationDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == due30Student).AnnualReviewDueDate = today.AddDays(10);
            seedCtx.SchoolStudents.Single(s => s.Id == due30Student).ReevaluationDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == noLeadStudent).AnnualReviewDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == noLeadStudent).ReevaluationDueDate = today.AddDays(400);
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        var homeResult = await CreateService(ctx).GetForUserAsync(adminId);
        Assert.True(homeResult.Success, homeResult.Message);
        var summary = homeResult.Data!.Staff!.ComplianceSummary!;

        var districtService = new DistrictService(ctx, new OrgAccessService(ctx), NullLogger<DistrictService>.Instance);
        var boardResult = await districtService.GetComplianceBoardAsync(adminId, null, null, null);
        Assert.True(boardResult.Success, boardResult.Message);
        var board = boardResult.Data!.Summary;

        Assert.Equal(board.OverdueAnnual, summary.OverdueAnnual);
        Assert.Equal(board.OverdueReeval, summary.OverdueReeval);
        Assert.Equal(board.Due30, summary.Due30);
        Assert.Equal(board.Due60, summary.Due60);
        Assert.Equal(board.DueInRange, summary.DueInRange);
        Assert.Equal(board.UnknownDates, summary.UnknownDates);
        Assert.Equal(board.NoLead, summary.NoLead);
        Assert.Equal(board.ActiveStudents, summary.ActiveStudents);

        // Sanity: the buckets actually caught something (not a trivially-equal all-zero comparison).
        Assert.True(summary.OverdueAnnual >= 1);
        Assert.True(summary.Due30 >= 1);
        Assert.True(summary.NoLead >= 1);
    }

    /// <summary>Review-fix contract, todos/082: OverdueByCaseManager is capped like every sibling home
    /// list, with OverdueByCaseManagerTotal carrying the true (uncapped) count.</summary>
    [Fact]
    public async Task StaffHome_DistrictAdmin_OverdueByCaseManagerCappedAt50_WithTotal()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (adminId, _) = _db.Staff("cap-admin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        var (leadId, _) = _db.Staff("cap-lead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);

        const int overdueStudentCount = 55;
        using (var seedCtx = _db.Context())
        {
            for (var i = 0; i < overdueStudentCount; i++)
            {
                var student = new SchoolStudent
                {
                    SchoolId = schoolId,
                    DistrictId = districtId,
                    FirstName = $"Student{i:D3}",
                    LastName = "Overdue",
                    Status = StudentStatus.Active,
                    CaseManagerUserId = leadId,
                    AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1),
                    ReevaluationDueDate = DateTime.UtcNow.Date.AddDays(400)
                };
                seedCtx.SchoolStudents.Add(student);
            }
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(adminId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Staff!;
        Assert.Equal(50, home.OverdueByCaseManager!.Count);
        Assert.True(home.OverdueByCaseManagerTotal >= overdueStudentCount);
    }

    /// <summary>Review-fix contract, todos/086 P3 #3: a SchoolAdmin still bound to a since-deactivated
    /// school sees the same empty picture the compliance board would show for that school, not a stale
    /// non-zero one (LoadScopedStudentsAsync + HomeService.ScopedActiveStudents both now exclude it).</summary>
    [Fact]
    public async Task StaffHome_SchoolAdmin_InactiveBoundSchool_RosterAttentionStaysEmpty()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A", isActive: false);
        var (schoolAdminId, _) = _db.Staff("inactive-school-home-admin@example.com", districtId, schoolId, Models.OrgRoleIds.SchoolAdmin);
        var studentId = _db.Student(schoolId, "Sam", "Student");
        using (var seedCtx = _db.Context())
        {
            seedCtx.SchoolStudents.Single(s => s.Id == studentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-5);
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForUserAsync(schoolAdminId);

        Assert.True(result.Success, result.Message);
        var home = result.Data!.Staff!;
        Assert.Equal(StaffHomeVariant.SchoolAdmin, home.Variant);
        Assert.NotNull(home.RosterAttention);
        Assert.Equal(0, home.RosterAttention!.OverdueAnnual);
        Assert.Equal(0, home.RosterAttention.NoLead);
        Assert.Equal(0, home.RosterAttention.NoFamily);
        Assert.Empty(home.OverdueByCaseManager!);
        Assert.Equal(0, home.OverdueByCaseManagerTotal);
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

        // Admin path: profile/scope label, meetings-this-week, drafts, the two plan-6 shared-draft queries
        // (sharedDraftsAwaitingFamily, familyResponsesToReview), obligations-for-scope (shared
        // StaffContext, no re-lookup), the no-filter compliance board (supplies NoLead too), noFamily —
        // was pinned at 9 after plan 6; plan 7 adds 3 fixed (not per-student) obligation queries to every
        // ComputeAndFilterAsync call (GoalRecords, EvaluationCases, EvaluatorAssignments), so this is
        // now measured at 12.
        var counter = new DbActivityCounter();
        using (var ctx = _db.Context(counter))
        {
            var result = await CreateService(ctx).GetForUserAsync(adminId);
            Assert.True(result.Success, result.Message);
        }
        Assert.True(counter.Queries <= 12, $"DistrictAdmin home issued {counter.Queries} queries");

        // Staff-tier path: profile/scope label, meetings-this-week, lead-only obligations, drafts, plus
        // the same two plan-6 shared-draft queries — was 7 (was 5 before plan 6); plan 7 phases 1-2's 3
        // fixed obligation queries (see above) brought this to 10; phase 4's CaseManager-scoped unsigned
        // finalized documents list adds one more, now 11.
        counter.Reset();
        using (var ctx = _db.Context(counter))
        {
            var result = await CreateService(ctx).GetForUserAsync(leadId);
            Assert.True(result.Success, result.Message);
        }
        Assert.True(counter.Queries <= 11, $"CaseManager home issued {counter.Queries} queries");

        // Parent and student paths are lighter still (no obligation/roster computation at all). Parent
        // gains one plan-6 query (unacknowledged Active shared-draft revisions) — measured at 7 (was 6).
        var parentUserId = _db.SeedUser("boundsparent@example.com", UserRole.Parent, "Pat", "Parent");
        var childId = _db.ChildProfile(parentUserId, "Kid", "Bounds");
        _db.ChildLink(studentId, childId);

        counter.Reset();
        using (var ctx = _db.Context(counter))
        {
            var result = await CreateService(ctx).GetForUserAsync(parentUserId);
            Assert.True(result.Success, result.Message);
        }
        Assert.True(counter.Queries <= 7, $"Parent home issued {counter.Queries} queries");

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
