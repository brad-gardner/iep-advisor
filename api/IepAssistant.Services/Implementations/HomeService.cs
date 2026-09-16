using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// One server-computed home per role (see <see cref="IHomeService"/>, plan 5 decision 1). Dispatch order:
/// an ACTIVE <see cref="StaffProfile"/> wins first (a district admin who is also a parent still gets
/// their staff home); otherwise <see cref="User.Role"/> picks Student vs. Parent (every non-Student role
/// reaching here — Parent, and the rare Admin — gets the parent-shaped home, which degrades gracefully
/// to empty lists + a setup notice when there is nothing to show).
///
/// <para>Query budget: each branch stays near ~5-7 round trips regardless of how many meetings/drafts/
/// students are in scope — draft completeness batches ALL distinct pinned template versions in one query
/// instead of one per draft, and the admin roster/board counts reuse <see cref="IObligationService"/>'s
/// already-computed obligation list instead of re-querying per bucket.</para>
/// </summary>
public class HomeService : IHomeService
{
    private const string HomeTimeZoneId = "America/New_York";
    private const int MaxDrafts = 20;
    private const int MaxParentDocuments = 10;
    private const int MaxProgressReports = 5;

    /// <summary>Plan 6: sibling cap to <see cref="MaxDrafts"/> for the two shared-draft home lists.</summary>
    private const int MaxSharedDrafts = 10;

    /// <summary>Sibling cap to <see cref="MaxDrafts"/>/<see cref="MaxParentDocuments"/>/
    /// <see cref="MaxProgressReports"/> — the only home list that was previously unbounded
    /// (review-fix contract, todos/082).</summary>
    internal const int MaxOverdueByCaseManager = 50;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IObligationService _obligationService;
    private readonly IDocumentCompletenessService _completeness;
    private readonly IDistrictService _districtService;

    public HomeService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IObligationService obligationService,
        IDocumentCompletenessService completeness,
        IDistrictService districtService)
    {
        _context = context;
        _orgAccess = orgAccess;
        _obligationService = obligationService;
        _completeness = completeness;
        _districtService = districtService;
    }

    public async Task<ServiceResult<HomeModel>> GetForUserAsync(int userId, CancellationToken ct = default)
    {
        var staffCtx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (staffCtx != null)
        {
            var staffHome = await BuildStaffHomeAsync(userId, staffCtx, ct);
            return ServiceResult<HomeModel>.SuccessResult(new HomeModel
            {
                Kind = HomeKind.Staff,
                GeneratedAt = DateTime.UtcNow,
                Staff = staffHome
            });
        }

        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName, u.Role })
            .FirstOrDefaultAsync(ct);
        if (user == null)
            return ServiceResult<HomeModel>.FailureResult("User not found.");

        var displayName = (user.FirstName + " " + user.LastName).Trim();

        if (user.Role == UserRole.Student)
        {
            var studentHome = await BuildStudentHomeAsync(userId, displayName, ct);
            return ServiceResult<HomeModel>.SuccessResult(new HomeModel
            {
                Kind = HomeKind.Student,
                GeneratedAt = DateTime.UtcNow,
                Student = studentHome
            });
        }

        var parentHome = await BuildParentHomeAsync(userId, displayName, ct);
        return ServiceResult<HomeModel>.SuccessResult(new HomeModel
        {
            Kind = HomeKind.Parent,
            GeneratedAt = DateTime.UtcNow,
            Parent = parentHome
        });
    }

    // ================================================================= Staff

    private async Task<StaffHomeModel> BuildStaffHomeAsync(int userId, StaffContext ctx, CancellationToken ct)
    {
        var variant = ctx.OrgRoleId switch
        {
            OrgRoleIds.RelatedServiceProvider => StaffHomeVariant.Provider,
            OrgRoleIds.GeneralEducator => StaffHomeVariant.GeneralEducator,
            OrgRoleIds.SchoolAdmin => StaffHomeVariant.SchoolAdmin,
            OrgRoleIds.DistrictAdmin => StaffHomeVariant.DistrictAdmin,
            _ => StaffHomeVariant.CaseManager
        };
        var isAdmin = OrgRoleIds.IsAdmin(ctx.OrgRoleId);

        var profile = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                p.User.FirstName,
                p.User.LastName,
                DistrictName = p.District.Name,
                SchoolName = p.School != null ? p.School.Name : null
            })
            .FirstAsync(ct);

        var (weekStartUtc, weekEndUtc, weekStart, weekEnd) = ComputeThisWeek(DateTime.UtcNow);

        var home = new StaffHomeModel
        {
            Variant = variant,
            DisplayName = (profile.FirstName + " " + profile.LastName).Trim(),
            ScopeLabel = profile.SchoolName ?? profile.DistrictName,
            WeekStart = weekStart,
            WeekEnd = weekEnd
        };

        // ---- This week's meetings: staff-tier = where I'm a participant; admins = every Scheduled
        // meeting in their scope (SchoolAdmin without a bound school has nothing in scope). ----
        var meetingsQuery = _context.Meetings.AsNoTracking()
            .Where(m => m.Status == MeetingStatus.Scheduled && m.StartsAtUtc >= weekStartUtc && m.StartsAtUtc < weekEndUtc);

        if (!isAdmin)
        {
            meetingsQuery = meetingsQuery.Where(m => m.Participants.Any(p => p.UserId == userId));
        }
        else if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            meetingsQuery = meetingsQuery.Where(m => m.SchoolStudent.School.DistrictId == ctx.DistrictId && m.SchoolStudent.School.IsActive);
        }
        else if (ctx.SchoolId.HasValue)
        {
            meetingsQuery = meetingsQuery.Where(m => m.SchoolStudent.SchoolId == ctx.SchoolId.Value);
        }
        else
        {
            meetingsQuery = meetingsQuery.Where(_ => false);
        }

        home.MeetingsThisWeek = await meetingsQuery
            .OrderBy(m => m.StartsAtUtc)
            .Select(m => new HomeMeetingModel
            {
                Id = m.Id,
                Title = m.Title,
                Type = m.Type,
                StartsAtUtc = m.StartsAtUtc,
                TimeZoneId = m.TimeZoneId,
                DurationMinutes = m.DurationMinutes,
                StudentId = m.SchoolStudentId,
                StudentName = (m.SchoolStudent.FirstName + " " + m.SchoolStudent.LastName).Trim(),
                MyInviteStatus = m.Participants.Where(p => p.UserId == userId).Select(p => (InviteStatus?)p.InviteStatus).FirstOrDefault(),
                Status = m.Status
            })
            .ToListAsync(ct);

        // ---- Obligations: lead-only, DueSoon+Overdue. Admins get none here — the compliance board covers scope-wide obligations. ----
        if (!isAdmin)
        {
            var leadResult = await _obligationService.GetLeadOnlyAsync(userId, ct);
            home.Obligations = (leadResult.Data ?? new List<ObligationModel>())
                .Where(o => o.Status is ObligationStatus.Overdue or ObligationStatus.DueSoon)
                .ToList();
        }

        // ---- Drafts: Draft DocumentInstances where I currently have access — an active team member, an
        // active SchoolStudentAccess row, or (admin variants) the student is in my scope. "I last edited
        // it" is NOT a visibility grant on its own (a caller who is removed from a team, or whose access
        // is superseded by a transfer, must lose the draft too, matching CanActOnStudentAsync) — it's kept
        // only as a secondary ordering signal below (review-fix contract, todos/080). ----
        IQueryable<DocumentInstance> draftsQuery = _context.DocumentInstances.AsNoTracking()
            .Where(i => i.Status == DocumentInstanceStatus.Draft);
        if (isAdmin)
        {
            var scopedStudentIds = ScopedActiveStudents(ctx).Select(s => s.Id);
            draftsQuery = draftsQuery.Where(i =>
                _context.StudentTeamMembers.Any(m => m.SchoolStudentId == i.SchoolStudentId && m.IsActive && m.UserId == userId)
                || _context.SchoolStudentAccesses.Any(a => a.SchoolStudentId == i.SchoolStudentId && a.IsActive && a.UserId == userId)
                || scopedStudentIds.Contains(i.SchoolStudentId));
        }
        else
        {
            draftsQuery = draftsQuery.Where(i =>
                _context.StudentTeamMembers.Any(m => m.SchoolStudentId == i.SchoolStudentId && m.IsActive && m.UserId == userId)
                || _context.SchoolStudentAccesses.Any(a => a.SchoolStudentId == i.SchoolStudentId && a.IsActive && a.UserId == userId));
        }

        var draftsRaw = await draftsQuery
            .OrderByDescending(i => i.LastEditedAt)
            .ThenByDescending(i => i.LastEditedByUserId == userId)
            .Take(MaxDrafts)
            .Select(i => new
            {
                i.Id,
                i.SchoolStudentId,
                StudentName = (i.SchoolStudent.FirstName + " " + i.SchoolStudent.LastName).Trim(),
                DocumentTypeKey = i.DocumentType.Key,
                DocumentTypeDisplayName = i.DocumentType.DisplayName,
                i.DocumentTemplateVersionId,
                i.ValuesJson,
                i.LastEditedAt
            })
            .ToListAsync(ct);

        if (draftsRaw.Count > 0)
        {
            // Batch every distinct pinned template version's section/field tree in ONE query, instead of
            // one ITemplateAuthoringService round trip per draft — keeps the whole home bounded regardless
            // of how many drafts the caller has in progress.
            var versionIds = draftsRaw.Select(d => d.DocumentTemplateVersionId).Distinct().ToList();
            var sectionsByVersion = await LoadSectionsByVersionAsync(versionIds, ct);

            home.Drafts = draftsRaw.Select(d =>
            {
                var sections = sectionsByVersion.TryGetValue(d.DocumentTemplateVersionId, out var s) ? s : new List<TemplateSectionModel>();
                var completeness = _completeness.Compute(sections, d.ValuesJson);
                return new HomeDraftModel
                {
                    InstanceId = d.Id,
                    StudentId = d.SchoolStudentId,
                    StudentName = d.StudentName,
                    DocumentTypeKey = d.DocumentTypeKey,
                    DocumentTypeDisplayName = d.DocumentTypeDisplayName,
                    LastEditedAt = d.LastEditedAt,
                    CompletenessPercent = completeness.Percent,
                    RequiredMissing = completeness.RequiredMissing
                };
            }).ToList();
        }

        // ---- Plan 6: shared drafts awaiting a family acknowledgement/response, and open family
        // responses to review — same "my students" scoping as Drafts above (team member, active access
        // grant, or — for admin variants — anywhere in scope). ----
        IQueryable<SharedDraftRevision> myActiveRevisions = _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => r.Status == SharedDraftStatus.Active);
        if (isAdmin)
        {
            var scopedStudentIds = ScopedActiveStudents(ctx).Select(s => s.Id);
            myActiveRevisions = myActiveRevisions.Where(r =>
                _context.StudentTeamMembers.Any(m => m.SchoolStudentId == r.DocumentInstance.SchoolStudentId && m.IsActive && m.UserId == userId)
                || _context.SchoolStudentAccesses.Any(a => a.SchoolStudentId == r.DocumentInstance.SchoolStudentId && a.IsActive && a.UserId == userId)
                || scopedStudentIds.Contains(r.DocumentInstance.SchoolStudentId));
        }
        else
        {
            myActiveRevisions = myActiveRevisions.Where(r =>
                _context.StudentTeamMembers.Any(m => m.SchoolStudentId == r.DocumentInstance.SchoolStudentId && m.IsActive && m.UserId == userId)
                || _context.SchoolStudentAccesses.Any(a => a.SchoolStudentId == r.DocumentInstance.SchoolStudentId && a.IsActive && a.UserId == userId));
        }

        home.SharedDraftsAwaitingFamily = await myActiveRevisions
            .Where(r => !_context.DraftAcknowledgements.Any(a => a.SharedDraftRevisionId == r.Id)
                     && !_context.DraftResponses.Any(x => x.SharedDraftRevisionId == r.Id))
            .OrderByDescending(r => r.SharedAt)
            .Take(MaxSharedDrafts)
            .Select(r => new HomeSharedDraftModel
            {
                InstanceId = r.DocumentInstanceId,
                StudentId = r.DocumentInstance.SchoolStudentId,
                StudentName = (r.DocumentInstance.SchoolStudent.FirstName + " " + r.DocumentInstance.SchoolStudent.LastName).Trim(),
                SharedAt = r.SharedAt,
                RespondedAt = null
            })
            .ToListAsync(ct);

        home.FamilyResponsesToReview = await myActiveRevisions
            .Where(r => _context.DraftResponses.Any(x => x.SharedDraftRevisionId == r.Id && x.Status == DraftResponseStatus.Open))
            .OrderByDescending(r => r.SharedAt)
            .Take(MaxSharedDrafts)
            .Select(r => new HomeSharedDraftModel
            {
                InstanceId = r.DocumentInstanceId,
                StudentId = r.DocumentInstance.SchoolStudentId,
                StudentName = (r.DocumentInstance.SchoolStudent.FirstName + " " + r.DocumentInstance.SchoolStudent.LastName).Trim(),
                SharedAt = r.SharedAt,
                RespondedAt = _context.DraftResponses
                    .Where(x => x.SharedDraftRevisionId == r.Id && x.Status == DraftResponseStatus.Open)
                    .Max(x => (DateTime?)x.CreatedAt)
            })
            .ToListAsync(ct);

        if (isAdmin)
            await PopulateAdminSectionsAsync(home, userId, ctx, variant, ct);

        return home;
    }

    /// <summary>Roster attention counts, the overdue/at-risk-by-student table, and (DistrictAdmin only)
    /// the compliance summary. Takes the already-resolved <paramref name="ctx"/> and passes it straight
    /// into <see cref="IObligationService.GetForScopeAsync(StaffContext, int?, ObligationStatus?, CancellationToken)"/>
    /// (no re-lookup), and reuses that call's scoped active-student ids for the NoLead/NoFamily counts
    /// instead of a second from-scratch scoped query (review-fix contract, todos/074). For DistrictAdmin,
    /// ComplianceSummary is the SAME <see cref="ComplianceSummaryModel"/> instance
    /// <see cref="IDistrictService.GetComplianceBoardAsync"/> computes with no filters — not an
    /// independently re-derived aggregation — so the two surfaces can never numerically drift apart
    /// (review-fix contract, todos/078); NoLead is likewise reused from that board summary rather than
    /// counted twice.</summary>
    private async Task PopulateAdminSectionsAsync(StaffHomeModel home, int userId, StaffContext ctx, StaffHomeVariant variant, CancellationToken ct)
    {
        home.UnsignedFinalized = new List<HomeUnsignedModel>();

        // SchoolAdmin with no school binding has nothing to oversee — a valid empty payload (mirrors
        // DistrictService.GetDashboardAsync for the same case), not an error.
        if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin && ctx.SchoolId == null)
        {
            home.RosterAttention = new RosterAttentionModel();
            home.OverdueByCaseManager = new List<CaseManagerRowModel>();
            home.OverdueByCaseManagerTotal = 0;
            return;
        }

        var scopeResult = await _obligationService.GetForScopeAsync(ctx, null, null, ct);
        var obligations = scopeResult.Data ?? new List<ObligationModel>();
        var today = DateTime.UtcNow.Date;

        // Every active student in scope carries at least an AnnualReview + Reevaluation obligation
        // (ObligationService.ComputeForStudent), so the distinct id set below IS the active-student set —
        // reused for the NoLead/NoFamily counts instead of a second ScopedActiveStudents(ctx) query.
        // (DistrictAdmin's ComplianceSummary.ActiveStudents comes from the board summary itself, below.)
        var activeStudentIds = obligations.Select(o => o.SchoolStudentId).Distinct().ToHashSet();

        int overdueAnnual, overdueReeval, due30, unknownDates, noLead;
        ComplianceSummaryModel? boardSummary = null;

        if (variant == StaffHomeVariant.DistrictAdmin)
        {
            // "Same numbers as the board with no filters" (plan 5 contract) — call the actual board
            // computation instead of re-deriving these six counts from the obligations list, so a
            // DistrictAdmin's home ComplianceSummary can never disagree with GetComplianceBoardAsync's
            // Summary for the same district. Also supplies NoLead below, so PopulateAdminSectionsAsync
            // doesn't need its own NoLead query for this variant.
            var boardResult = await _districtService.GetComplianceBoardAsync(userId, null, null, null, ct);
            boardSummary = boardResult.Data?.Summary ?? new ComplianceSummaryModel();
            overdueAnnual = boardSummary.OverdueAnnual;
            overdueReeval = boardSummary.OverdueReeval;
            due30 = boardSummary.Due30;
            unknownDates = boardSummary.UnknownDates;
            noLead = boardSummary.NoLead;
        }
        else
        {
            overdueAnnual = obligations.Count(o => o.Kind == ObligationKind.AnnualReview && o.Status == ObligationStatus.Overdue);
            overdueReeval = obligations.Count(o => o.Kind == ObligationKind.Reevaluation && o.Status == ObligationStatus.Overdue);
            due30 = obligations
                .Where(o => o.Kind != ObligationKind.EtrDue && o.DueDate.HasValue && o.DueDate.Value >= today && o.DueDate.Value <= today.AddDays(30))
                .Select(o => o.SchoolStudentId).Distinct().Count();
            unknownDates = obligations
                .Where(o => o.Kind != ObligationKind.EtrDue && o.Status == ObligationStatus.Unknown)
                .Select(o => o.SchoolStudentId).Distinct().Count();

            var scopedByActiveIds = _context.SchoolStudents.AsNoTracking().Where(s => activeStudentIds.Contains(s.Id));
            noLead = await scopedByActiveIds.CountAsync(StudentAttentionRules.NoLead(_context, ctx.DistrictId), ct);
        }

        var noFamilyQuery = _context.SchoolStudents.AsNoTracking().Where(s => activeStudentIds.Contains(s.Id));
        var noFamily = await noFamilyQuery.CountAsync(StudentAttentionRules.NoFamily(_context), ct);

        home.RosterAttention = new RosterAttentionModel
        {
            NoLead = noLead,
            NoFamily = noFamily,
            UnknownDates = unknownDates,
            OverdueAnnual = overdueAnnual,
            OverdueReeval = overdueReeval,
            Due30 = due30
        };

        // Sorted by STUDENT name — never by staff (no ranking language/per-staff scores anywhere here).
        // Capped like every sibling home list (drafts/documents/reports); OverdueByCaseManagerTotal
        // carries the true count for a "view all in compliance board" link (review-fix contract, todos/082).
        var overdueByCaseManagerAll = obligations
            .Where(o => o.Kind != ObligationKind.EtrDue && o.Status is ObligationStatus.Overdue or ObligationStatus.DueSoon)
            .OrderBy(o => o.StudentName)
            .ToList();
        home.OverdueByCaseManagerTotal = overdueByCaseManagerAll.Count;
        home.OverdueByCaseManager = overdueByCaseManagerAll
            .Take(MaxOverdueByCaseManager)
            .Select(o => new CaseManagerRowModel
            {
                StudentId = o.SchoolStudentId,
                StudentName = o.StudentName,
                CaseManagerName = o.OwnerName,
                Kind = o.Kind,
                DueDate = o.DueDate,
                Status = o.Status
            })
            .ToList();

        if (variant == StaffHomeVariant.DistrictAdmin)
            home.ComplianceSummary = boardSummary;
    }

    /// <summary>Active students in scope, in an ACTIVE school (matches
    /// <see cref="DistrictService"/>'s ScopedActiveSchools filter — a SchoolAdmin still bound to a
    /// since-deactivated school must see the same empty picture the compliance board shows for it, not a
    /// stale non-zero one; review-fix contract, todos/086 P3 #3). A SchoolAdmin with no school binding has
    /// nothing in scope (empty, not a throw).</summary>
    private IQueryable<SchoolStudent> ScopedActiveStudents(StaffContext ctx)
    {
        IQueryable<SchoolStudent> query;
        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
            query = _context.SchoolStudents.AsNoTracking().Where(s => s.School.DistrictId == ctx.DistrictId && s.School.IsActive);
        else if (ctx.SchoolId.HasValue)
            query = _context.SchoolStudents.AsNoTracking().Where(s => s.SchoolId == ctx.SchoolId.Value && s.School.IsActive);
        else
            query = _context.SchoolStudents.AsNoTracking().Where(_ => false);
        return query.Where(s => s.Status == StudentStatus.Active);
    }

    // ================================================================= Parent

    private async Task<ParentHomeModel> BuildParentHomeAsync(int userId, string displayName, CancellationToken ct)
    {
        var home = new ParentHomeModel { DisplayName = displayName };

        // Accepted, active ChildAccess -> ChildProfile, left-joined to its first accepted, active
        // ChildLink -> SchoolStudent (a child may have no school link yet).
        var children = await _context.ChildAccesses.AsNoTracking()
            .Where(ca => ca.UserId == userId && ca.IsActive && ca.AcceptedAt != null && ca.ChildProfile.IsActive)
            .OrderBy(ca => ca.ChildProfile.FirstName)
            .Select(ca => new
            {
                ChildId = ca.ChildProfileId,
                ChildFirstName = ca.ChildProfile.FirstName,
                ChildLastName = ca.ChildProfile.LastName,
                StudentId = _context.ChildLinks
                    .Where(l => l.ChildProfileId == ca.ChildProfileId && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null)
                    .Select(l => (int?)l.SchoolStudentId)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        home.Children = children.Select(c => new ParentChildModel
        {
            ChildId = c.ChildId,
            ChildName = (c.ChildFirstName + " " + c.ChildLastName).Trim(),
            HasSchoolLink = c.StudentId.HasValue,
            StudentId = c.StudentId
        }).ToList();

        if (children.Count == 0)
        {
            home.SetupNotices.Add("No children linked to your account yet.");
            return home;
        }

        foreach (var c in children.Where(c => !c.StudentId.HasValue))
            home.SetupNotices.Add($"{c.ChildFirstName} has no school link yet.");

        var linkedStudentIds = children.Where(c => c.StudentId.HasValue).Select(c => c.StudentId!.Value).Distinct().ToList();
        var childInfoByStudentId = children
            .Where(c => c.StudentId.HasValue)
            .GroupBy(c => c.StudentId!.Value)
            .ToDictionary(g => g.Key, g => (ChildId: g.First().ChildId, ChildName: (g.First().ChildFirstName + " " + g.First().ChildLastName).Trim()));

        if (linkedStudentIds.Count > 0)
        {
            var nowUtc = DateTime.UtcNow;
            var nextMeeting = await _context.Meetings.AsNoTracking()
                .Where(m => linkedStudentIds.Contains(m.SchoolStudentId) && m.Status == MeetingStatus.Scheduled && m.StartsAtUtc >= nowUtc)
                .OrderBy(m => m.StartsAtUtc)
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.Type,
                    m.StartsAtUtc,
                    m.TimeZoneId,
                    m.DurationMinutes,
                    m.SchoolStudentId,
                    m.Status,
                    MyInviteStatus = m.Participants.Where(p => p.UserId == userId).Select(p => (InviteStatus?)p.InviteStatus).FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);

            if (nextMeeting != null && childInfoByStudentId.TryGetValue(nextMeeting.SchoolStudentId, out var meetingChild))
            {
                home.NextMeeting = new ParentNextMeetingModel
                {
                    Id = nextMeeting.Id,
                    Title = nextMeeting.Title,
                    Type = nextMeeting.Type,
                    StartsAtUtc = nextMeeting.StartsAtUtc,
                    TimeZoneId = nextMeeting.TimeZoneId,
                    DurationMinutes = nextMeeting.DurationMinutes,
                    StudentId = nextMeeting.SchoolStudentId,
                    StudentName = meetingChild.ChildName,
                    MyInviteStatus = nextMeeting.MyInviteStatus,
                    Status = nextMeeting.Status,
                    ChildId = meetingChild.ChildId,
                    ChildName = meetingChild.ChildName,
                    DaysUntil = Math.Max(0, (nextMeeting.StartsAtUtc.Date - nowUtc.Date).Days)
                };
            }

            var finalizedDocs = await _context.AuthoredDocumentVersions.AsNoTracking()
                .Where(v => linkedStudentIds.Contains(v.SchoolStudentId))
                .OrderByDescending(v => v.FinalizedAt)
                .Take(MaxParentDocuments)
                .Select(v => new
                {
                    v.Id,
                    v.SchoolStudentId,
                    v.VersionNumber,
                    v.FinalizedAt,
                    DocumentTypeDisplayName = v.DocumentType.DisplayName
                })
                .ToListAsync(ct);

            var finalizedItems = finalizedDocs
                .Where(d => childInfoByStudentId.ContainsKey(d.SchoolStudentId))
                .Select(d =>
                {
                    var docChild = childInfoByStudentId[d.SchoolStudentId];
                    return new ParentDocumentModel
                    {
                        Kind = ParentDocumentKind.Finalized,
                        Id = d.Id,
                        ChildId = docChild.ChildId,
                        ChildName = docChild.ChildName,
                        DocumentTypeDisplayName = d.DocumentTypeDisplayName,
                        VersionNumber = d.VersionNumber,
                        Date = d.FinalizedAt,
                        LinkPath = $"/children/{docChild.ChildId}/authored-versions/{d.Id}"
                    };
                });

            // Plan 6: Active shared-draft revisions this parent has not yet acknowledged — the family's
            // "review this" queue is Finalized versions AND unacknowledged shared drafts together, newest
            // first, under the same overall cap.
            var sharedDraftRows = await _context.SharedDraftRevisions.AsNoTracking()
                .Where(r => linkedStudentIds.Contains(r.DocumentInstance.SchoolStudentId)
                         && r.Status == SharedDraftStatus.Active
                         && !_context.DraftAcknowledgements.Any(a => a.SharedDraftRevisionId == r.Id && a.UserId == userId))
                .OrderByDescending(r => r.SharedAt)
                .Take(MaxParentDocuments)
                .Select(r => new
                {
                    r.Id,
                    SchoolStudentId = r.DocumentInstance.SchoolStudentId,
                    r.RevisionNumber,
                    r.SharedAt,
                    DocumentTypeDisplayName = r.DocumentInstance.DocumentType.DisplayName
                })
                .ToListAsync(ct);

            var sharedDraftItems = sharedDraftRows
                .Where(d => childInfoByStudentId.ContainsKey(d.SchoolStudentId))
                .Select(d =>
                {
                    var docChild = childInfoByStudentId[d.SchoolStudentId];
                    return new ParentDocumentModel
                    {
                        Kind = ParentDocumentKind.SharedDraft,
                        Id = d.Id,
                        ChildId = docChild.ChildId,
                        ChildName = docChild.ChildName,
                        DocumentTypeDisplayName = d.DocumentTypeDisplayName,
                        VersionNumber = d.RevisionNumber,
                        Date = d.SharedAt,
                        LinkPath = $"/children/{docChild.ChildId}/shared-drafts/{d.Id}" // route takes the revision id, not the per-document number
                    };
                });

            home.DocumentsToReview = finalizedItems
                .Concat(sharedDraftItems)
                .OrderByDescending(d => d.Date)
                .Take(MaxParentDocuments)
                .ToList();
        }

        var childIds = children.Select(c => c.ChildId).ToList();
        var childNameByChildId = children.ToDictionary(c => c.ChildId, c => (c.ChildFirstName + " " + c.ChildLastName).Trim());
        var reports = await _context.ProgressReports.AsNoTracking()
            .Where(r => childIds.Contains(r.ChildProfileId) && r.IsActive)
            .OrderByDescending(r => r.CreatedAt)
            .Take(MaxProgressReports)
            .Select(r => new { r.Id, r.ChildProfileId, r.FileName, r.CreatedAt })
            .ToListAsync(ct);

        home.RecentProgressReports = reports.Select(r => new ParentProgressReportModel
        {
            Id = r.Id,
            ChildId = r.ChildProfileId,
            ChildName = childNameByChildId.TryGetValue(r.ChildProfileId, out var name) ? name : string.Empty,
            Title = r.FileName,
            CreatedAt = r.CreatedAt
        }).ToList();

        return home;
    }

    // ================================================================= Student

    private async Task<StudentHomeModel> BuildStudentHomeAsync(int userId, string displayName, CancellationToken ct)
    {
        var home = new StudentHomeModel { DisplayName = displayName };

        var linkedStudentId = await _context.StudentProfiles.AsNoTracking()
            .Where(sp => sp.UserId == userId)
            .Select(sp => sp.SchoolStudentId)
            .FirstOrDefaultAsync(ct);
        home.LinkedStudentId = linkedStudentId;

        if (linkedStudentId.HasValue)
        {
            var nowUtc = DateTime.UtcNow;
            var meeting = await _context.Meetings.AsNoTracking()
                .Where(m => m.SchoolStudentId == linkedStudentId.Value && m.Status == MeetingStatus.Scheduled && m.StartsAtUtc >= nowUtc)
                .OrderBy(m => m.StartsAtUtc)
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.Type,
                    m.StartsAtUtc,
                    m.TimeZoneId,
                    m.DurationMinutes,
                    m.Status,
                    StudentName = (m.SchoolStudent.FirstName + " " + m.SchoolStudent.LastName).Trim(),
                    MyInviteStatus = m.Participants.Where(p => p.UserId == userId).Select(p => (InviteStatus?)p.InviteStatus).FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);

            if (meeting != null)
            {
                home.NextMeeting = new HomeMeetingModel
                {
                    Id = meeting.Id,
                    Title = meeting.Title,
                    Type = meeting.Type,
                    StartsAtUtc = meeting.StartsAtUtc,
                    TimeZoneId = meeting.TimeZoneId,
                    DurationMinutes = meeting.DurationMinutes,
                    StudentId = linkedStudentId.Value,
                    StudentName = meeting.StudentName,
                    MyInviteStatus = meeting.MyInviteStatus,
                    Status = meeting.Status
                };
            }
        }

        var hasWorkspaceEntries = await _context.StudentWorkspaceEntries.AsNoTracking()
            .AnyAsync(e => e.StudentWorkspace.UserId == userId, ct);
        home.WorkspaceNudge = hasWorkspaceEntries
            ? null
            : "Add your strengths, interests, and goals to your self-advocacy workspace.";

        return home;
    }

    // ================================================================= Shared helpers

    private async Task<Dictionary<int, List<TemplateSectionModel>>> LoadSectionsByVersionAsync(List<int> versionIds, CancellationToken ct)
    {
        var sections = await _context.TemplateSections.AsNoTracking()
            .Where(s => versionIds.Contains(s.DocumentTemplateVersionId))
            .Include(s => s.Fields)
            .ToListAsync(ct);

        return sections
            .GroupBy(s => s.DocumentTemplateVersionId)
            .ToDictionary(g => g.Key, g => g.Select(s => new TemplateSectionModel
            {
                Id = s.Id,
                SectionKey = s.SectionKey,
                Title = s.Title,
                DisplayOrder = s.DisplayOrder,
                Fields = s.Fields.Select(f => new TemplateFieldModel
                {
                    Id = f.Id,
                    FieldKey = f.FieldKey,
                    FieldType = f.FieldType,
                    Label = f.Label,
                    Required = f.Required,
                    ConfigJson = f.ConfigJson,
                    DisplayOrder = f.DisplayOrder
                }).ToList()
            }).ToList());
    }

    /// <summary>Monday-Sunday in <see cref="HomeTimeZoneId"/> containing <paramref name="utcNow"/> —
    /// "this week" is America/New_York local calendar days, not a rolling 7x24h UTC window (plan 5).</summary>
    private static (DateTime WeekStartUtc, DateTime WeekEndUtc, DateTime WeekStart, DateTime WeekEnd) ComputeThisWeek(DateTime utcNow)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(HomeTimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        var daysSinceMonday = ((int)nowLocal.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6
        var mondayLocal = nowLocal.Date.AddDays(-daysSinceMonday);
        var sundayLocal = mondayLocal.AddDays(6);

        var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(mondayLocal, DateTimeKind.Unspecified), tz);
        // End is exclusive: the instant Monday-of-next-week begins, so a meeting anywhere on Sunday is included.
        var weekEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(sundayLocal.AddDays(1), DateTimeKind.Unspecified), tz);
        return (weekStartUtc, weekEndUtc, mondayLocal, sundayLocal);
    }
}
