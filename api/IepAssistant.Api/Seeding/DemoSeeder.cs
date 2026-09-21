using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.Seeding;

/// <summary>
/// Builds/removes the fictional "Maple Ridge Local Schools" (OH) demo district entirely through the
/// real application services (pilot-gates plan, phase 3, decision 5), so audit trail, goal-record
/// projection and the PDF queue all happen exactly as they would for a real district. See
/// <see cref="IDemoSeeder"/> for the CLI contract.
/// </summary>
public class DemoSeeder : IDemoSeeder
{
    public const string DemoPassword = "Demo!2026pw";

    /// <summary>The district itself, its staff, students and families are defined in <see cref="DemoRoster"/>.</summary>
    private const string DistrictName = DemoRoster.DistrictName;

    /// <summary>Reserved fictional domain every demo account uses — the anchor <see cref="ResetAsync"/>
    /// sweeps on, so it must never be a domain a real user could hold.</summary>
    private const string EmailDomain = DemoRoster.EmailDomain;

    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;
    private readonly IDistrictService _districtService;
    private readonly IStaffInviteService _staffInviteService;
    private readonly IEducatorService _educatorService;
    private readonly IStudentTeamService _studentTeamService;
    private readonly IDocumentInstanceService _documentInstanceService;
    private readonly IAuthoredDocumentVersionService _authoredDocumentVersionService;
    private readonly IGoalRecordService _goalRecordService;
    private readonly IMeetingService _meetingService;
    private readonly IMeetingDecisionService _meetingDecisionService;
    private readonly IChildLinkService _childLinkService;
    private readonly IStudentInviteService _studentInviteService;
    private readonly IDraftSharingService _draftSharingService;
    private readonly IDraftResponseService _draftResponseService;
    private readonly IEvaluationCaseService _evaluationCaseService;
    private readonly IFamilyContactService _familyContactService;
    private readonly AuthoredDocumentPdfQueue _pdfQueue;
    private readonly IBlobStorageService _blobStorage;
    private readonly ImmutabilityGuardBypass _immutabilityBypass;
    private readonly ILogger<DemoSeeder> _logger;

    public DemoSeeder(
        ApplicationDbContext context,
        IAuthService authService,
        IDistrictService districtService,
        IStaffInviteService staffInviteService,
        IEducatorService educatorService,
        IStudentTeamService studentTeamService,
        IDocumentInstanceService documentInstanceService,
        IAuthoredDocumentVersionService authoredDocumentVersionService,
        IGoalRecordService goalRecordService,
        IMeetingService meetingService,
        IMeetingDecisionService meetingDecisionService,
        IChildLinkService childLinkService,
        IStudentInviteService studentInviteService,
        IDraftSharingService draftSharingService,
        IDraftResponseService draftResponseService,
        IEvaluationCaseService evaluationCaseService,
        IFamilyContactService familyContactService,
        AuthoredDocumentPdfQueue pdfQueue,
        IBlobStorageService blobStorage,
        ImmutabilityGuardBypass immutabilityBypass,
        ILogger<DemoSeeder> logger)
    {
        _context = context;
        _authService = authService;
        _districtService = districtService;
        _staffInviteService = staffInviteService;
        _educatorService = educatorService;
        _studentTeamService = studentTeamService;
        _documentInstanceService = documentInstanceService;
        _authoredDocumentVersionService = authoredDocumentVersionService;
        _goalRecordService = goalRecordService;
        _meetingService = meetingService;
        _meetingDecisionService = meetingDecisionService;
        _childLinkService = childLinkService;
        _studentInviteService = studentInviteService;
        _draftSharingService = draftSharingService;
        _draftResponseService = draftResponseService;
        _evaluationCaseService = evaluationCaseService;
        _familyContactService = familyContactService;
        _pdfQueue = pdfQueue;
        _blobStorage = blobStorage;
        _immutabilityBypass = immutabilityBypass;
        _logger = logger;
    }

    // ================================================================================= SeedAsync

    public async Task<DemoSeedResult> SeedAsync(CancellationToken ct = default)
    {
        // See ResetAsync for why this is raised well above the 30s ADO.NET default.
        _context.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));

        var existing = await _context.Districts.AsNoTracking().FirstOrDefaultAsync(d => d.IsDemo, ct);
        if (existing != null)
            return DemoSeedResult.NoOpResult(
                $"Demo district '{existing.Name}' (id {existing.Id}) already exists — nothing to do. Run `seed-demo --fresh` to rebuild it, or `seed-demo --reset` to remove it.");

        DemoRoster.Validate();

        var logins = new List<DemoLoginRow>();

        _logger.LogInformation("Demo seed: creating district + admin…");
        var (districtId, districtAdminUserId) = await CreateDistrictAndAdminAsync(logins, ct);
        _logger.LogInformation("Demo seed: creating schools…");
        var schoolIds = await CreateSchoolsAsync(districtAdminUserId, ct);
        _logger.LogInformation("Demo seed: creating staff…");
        var staff = await CreateStaffAsync(districtAdminUserId, schoolIds, logins, ct);
        _logger.LogInformation("Demo seed: creating students + IEP teams…");
        var students = await CreateStudentsAsync(districtAdminUserId, schoolIds, staff, ct);

        // Order matters, and it is the order a district lives in: families are linked before meetings are
        // scheduled (so a meeting's default participant list includes the parent and the student account
        // the way it would in real use), and the team meets before this year's IEP is finalized (so a
        // finalized document's PDF header carries the date of the meeting behind it).
        _logger.LogInformation("Demo seed: linking families + the student account…");
        await LinkFamiliesAsync(students, logins, ct);
        _logger.LogInformation("Demo seed: creating meetings…");
        var meetings = await CreateMeetingsAsync(districtAdminUserId, students, ct);
        _logger.LogInformation("Demo seed: creating documents (IEPs/ETRs) + goals…");
        var documents = await CreateDocumentsAsync(districtAdminUserId, students, ct);
        _logger.LogInformation("Demo seed: creating evaluation cases…");
        var evaluationCases = await CreateEvaluationCasesAsync(districtAdminUserId, students, ct);
        _logger.LogInformation("Demo seed: sharing drafts with the engaged families…");
        await ShareDraftsWithFamiliesAsync(students, ct);
        _logger.LogInformation("Demo seed: recording family contact attempts…");
        await CreateContactAttemptsAsync(districtAdminUserId, students, ct);

        var message =
            $"Seeded demo district '{DistrictName}' (id {districtId}): {schoolIds.Count} schools, " +
            $"{staff.Count + 1} staff, {students.Count} students, " +
            $"{documents.FinalizedIeps} finalized IEPs, {documents.FinalizedEtrs + evaluationCases.FinalizedEtrs} finalized ETRs, " +
            $"{documents.DraftIeps + evaluationCases.DraftIeps} IEPs in progress, {evaluationCases.Cases} evaluation cases, " +
            $"{meetings} meetings, {DemoRoster.Parents.Length} families, 1 student account.";
        return DemoSeedResult.Ok(message, logins);
    }

    // ================================================================================= District + admin

    private async Task<(int DistrictId, int AdminUserId)> CreateDistrictAndAdminAsync(List<DemoLoginRow> logins, CancellationToken ct)
    {
        var admin = DemoRoster.DistrictAdmin;
        var register = await _authService.RegisterDistrictAsync(new RegisterDistrictModel
        {
            Email = admin.Email,
            Password = DemoPassword,
            FirstName = admin.FirstName,
            LastName = admin.LastName,
            DistrictName = DistrictName,
            StateCode = DemoRoster.StateCode
        }, ct);

        if (!register.Success || register.AuthResult == null)
            throw new InvalidOperationException($"Failed to create demo district: {register.Message}");

        var adminUserId = register.AuthResult.User.Id;
        var districtId = await _context.StaffProfiles
            .Where(sp => sp.UserId == adminUserId)
            .Select(sp => sp.DistrictId)
            .FirstAsync(ct);

        // Tag as the demo district + set the C11 adoption-slice flags. No service exists (rightly — no
        // real district should ever flip IsDemo) so this is a direct write on the row this seeder itself
        // just created via the real registration service.
        var district = await _context.Districts.FirstAsync(d => d.Id == districtId, ct);
        district.IsDemo = true;
        district.MagicLinkEnabled = true;
        district.RequireMfaForMagicLink = true;
        await _context.SaveChangesAsync(ct);

        logins.Add(new DemoLoginRow(admin.Title, admin.Email, DemoPassword));
        return (districtId, adminUserId);
    }

    // ================================================================================= Schools

    private async Task<List<int>> CreateSchoolsAsync(int districtAdminUserId, CancellationToken ct)
    {
        var ids = new List<int>();
        foreach (var school in DemoRoster.Schools)
        {
            var result = await _districtService.CreateSchoolAsync(districtAdminUserId,
                new CreateSchoolModel { Name = school.Name, StateCode = DemoRoster.StateCode }, ct);
            if (!result.Success || result.Data == null)
                throw new InvalidOperationException($"Failed to create school '{school.Name}': {result.Message}");
            ids.Add(result.Data.Id);
        }
        return ids;
    }

    // ================================================================================= Staff

    internal sealed record DemoStaff(int UserId, int StaffProfileId, int OrgRoleId, int? SchoolId, string Email, string FirstName, string LastName, string Title)
    {
        public string FullName => $"{FirstName} {LastName}";
    }

    private async Task<List<DemoStaff>> CreateStaffAsync(int districtAdminUserId, List<int> schoolIds, List<DemoLoginRow> logins, CancellationToken ct)
    {
        var staff = new List<DemoStaff>();
        foreach (var spec in DemoRoster.Staff)
        {
            var created = await InviteAndAcceptStaffAsync(districtAdminUserId, spec, schoolIds[spec.SchoolIndex!.Value], ct);
            staff.Add(created);
            logins.Add(new DemoLoginRow(spec.Title, created.Email, DemoPassword));
        }
        return staff;
    }

    private async Task<DemoStaff> InviteAndAcceptStaffAsync(int callerUserId, DemoRoster.StaffSpec spec, int schoolId, CancellationToken ct)
    {
        var invite = await _staffInviteService.InviteAsync(callerUserId,
            new CreateStaffInviteModel { Email = spec.Email, OrgRoleId = spec.OrgRoleId, SchoolId = schoolId }, ct);
        if (!invite.Success)
            throw new InvalidOperationException($"Failed to invite staff '{spec.Email}': {invite.Message}");

        var rawToken = await ExtractLatestTokenAsync(spec.Email, "StaffInvite", ct);
        var accept = await _staffInviteService.AcceptAsync(new AcceptStaffInviteModel
        {
            Token = rawToken,
            FirstName = spec.FirstName,
            LastName = spec.LastName,
            Password = DemoPassword
        }, ct);
        if (!accept.Success || accept.AuthResult == null)
            throw new InvalidOperationException($"Failed to accept staff invite for '{spec.Email}': {accept.Message}");

        var userId = accept.AuthResult.User.Id;
        var staffProfileId = await _context.StaffProfiles.Where(sp => sp.UserId == userId).Select(sp => sp.Id).FirstAsync(ct);
        return new DemoStaff(userId, staffProfileId, spec.OrgRoleId, schoolId, spec.Email, spec.FirstName, spec.LastName, spec.Title);
    }

    // ================================================================================= Students + teams

    /// <param name="TeamMemberNames">Name + team role of everyone on this student's team, for the
    /// "Meeting participants" table inside their documents.</param>
    internal sealed record DemoStudent(
        int Id,
        int Index,
        int SchoolId,
        int SchoolIndex,
        string FirstName,
        string LastName,
        GradeLevel Grade,
        DisabilityCategory? Disability,
        DemoRoster.Track Track,
        DemoStaff CaseManager,
        DemoStaff? Evaluator,
        IReadOnlyList<(string Name, string Role)> TeamMemberNames,
        DateTime? AnnualReviewDueDate,
        DateTime? EtrDate)
    {
        public string FullName => $"{FirstName} {LastName}";
        public bool GetsFinalizedIep => Track is DemoRoster.Track.EstablishedIep or DemoRoster.Track.IepWithRecentEtr;
        public string SchoolName => DemoRoster.Schools[SchoolIndex].Name;
    }

    private async Task<List<DemoStudent>> CreateStudentsAsync(int districtAdminUserId, List<int> schoolIds, List<DemoStaff> staff, CancellationToken ct)
    {
        var students = new List<DemoStudent>();
        var today = DateTime.UtcNow.Date;
        var caseManagerTurn = new Dictionary<int, int>();

        for (var i = 0; i < DemoRoster.Students.Length; i++)
        {
            var spec = DemoRoster.Students[i];
            var schoolId = schoolIds[spec.SchoolIndex];

            // Compliance dates by where the student is in the process. Annual reviews are spread from
            // 40 days overdue to 110 days out so the dashboard has genuinely overdue, due-soon and
            // comfortable rows rather than one uniform band.
            var reviewOffset = ReviewOffsetDays(i, DemoRoster.Students.Length);
            DateTime? iepDate = null, annualReviewDue = null, etrDate = null, reevaluationDue = null;
            switch (spec.Track)
            {
                case DemoRoster.Track.EstablishedIep:
                case DemoRoster.Track.IepWithRecentEtr:
                    iepDate = today.AddDays(reviewOffset - 365);
                    annualReviewDue = today.AddDays(reviewOffset);
                    etrDate = spec.Track == DemoRoster.Track.IepWithRecentEtr ? today.AddDays(-21) : today.AddDays(reviewOffset - 700);
                    reevaluationDue = etrDate.Value.AddYears(3);
                    break;
                case DemoRoster.Track.DraftIep:
                    // Newly eligible: an ETR on file, an IEP due within the month, none written yet.
                    etrDate = today.AddDays(-24);
                    reevaluationDue = etrDate.Value.AddYears(3);
                    annualReviewDue = today.AddDays(6 + i % 18);
                    break;
                case DemoRoster.Track.EvaluationEtrComplete:
                    etrDate = today.AddDays(-7);
                    reevaluationDue = etrDate.Value.AddYears(3);
                    annualReviewDue = today.AddDays(23);
                    break;
                case DemoRoster.Track.EvaluationInProgress:
                    break; // nothing on file yet
            }

            var createResult = await _educatorService.CreateStudentAsync(districtAdminUserId, new CreateSchoolStudentModel
            {
                FirstName = spec.FirstName,
                LastName = spec.LastName,
                DateOfBirth = BirthDateFor(spec.Grade, i, today),
                ExternalStudentId = $"MR-{1000 + i}",
                GradeLevel = spec.Grade,
                DisabilityCategory = spec.Disability,
                HomeLanguage = HomeLanguageFor(i),
                IepDate = iepDate,
                AnnualReviewDueDate = annualReviewDue,
                EtrDate = etrDate,
                ReevaluationDueDate = reevaluationDue,
                SchoolId = schoolId
            }, ct);

            if (!createResult.Success || createResult.Data == null)
                throw new InvalidOperationException($"Failed to create student '{spec.FirstName} {spec.LastName}': {createResult.Message}");

            var studentId = createResult.Data.Id;

            // ---- IEP team ----------------------------------------------------------------------
            // StudentTeamWriter.ValidateTeamCandidate requires every role except RelatedServiceProvider
            // to be based at THIS student's school, so the case manager, LEA representative and general
            // educator are all chosen from this building; the therapists and psychologist serve all three.
            var buildingCaseManagers = DemoRoster.CaseManagersAt(spec.SchoolIndex).ToList();
            var turn = caseManagerTurn.TryGetValue(spec.SchoolIndex, out var t) ? t : 0;
            caseManagerTurn[spec.SchoolIndex] = turn + 1;
            var caseManagerSpec = buildingCaseManagers[turn % buildingCaseManagers.Count];
            var caseManager = Match(staff, caseManagerSpec);

            var teamNames = new List<(string Name, string Role)>();
            await AddTeamMemberAsync(districtAdminUserId, studentId, caseManager, TeamRole.CaseManager, teamNames, isLead: true, ct);

            var principal = Match(staff, DemoRoster.Staff.First(s => s.OrgRoleId == OrgRoleIds.SchoolAdmin && s.SchoolIndex == spec.SchoolIndex));
            await AddTeamMemberAsync(districtAdminUserId, studentId, principal, TeamRole.LeaRepresentative, teamNames, isLead: false, ct);

            var generalEducator = Match(staff, DemoRoster.Staff.First(s => s.OrgRoleId == OrgRoleIds.GeneralEducator && s.SchoolIndex == spec.SchoolIndex));
            await AddTeamMemberAsync(districtAdminUserId, studentId, generalEducator, TeamRole.GeneralEducationTeacher, teamNames, isLead: false, ct);

            DemoStaff? evaluator = null;
            foreach (var (providerSpec, teamRole) in ProvidersFor(spec) )
            {
                var provider = Match(staff, providerSpec);
                await AddTeamMemberAsync(districtAdminUserId, studentId, provider, teamRole, teamNames, isLead: false, ct);
                evaluator ??= provider;
            }

            students.Add(new DemoStudent(
                studentId, i, schoolId, spec.SchoolIndex, spec.FirstName, spec.LastName, spec.Grade, spec.Disability,
                spec.Track, caseManager, evaluator ?? Match(staff, DemoRoster.Psychologist), teamNames,
                annualReviewDue, etrDate));

            if ((i + 1) % 10 == 0)
                _logger.LogInformation("Demo seed: created {Count}/{Total} students…", i + 1, DemoRoster.Students.Length);
        }

        return students;
    }

    /// <summary>The related-service staff a student's disability actually calls for, with the team role
    /// each of them holds. The school psychologist joins the teams of students being evaluated and those
    /// whose disability is identified through her assessments.</summary>
    private static IEnumerable<(DemoRoster.StaffSpec Spec, TeamRole Role)> ProvidersFor(DemoRoster.StudentSpec spec)
    {
        switch (spec.Disability)
        {
            case DisabilityCategory.SpeechOrLanguageImpairment:
                yield return (DemoRoster.Slp, TeamRole.SpeechLanguagePathologist);
                break;
            case DisabilityCategory.Autism:
                yield return (DemoRoster.Slp, TeamRole.SpeechLanguagePathologist);
                yield return (DemoRoster.Psychologist, TeamRole.SchoolPsychologist);
                break;
            case DisabilityCategory.DevelopmentalDelay:
                yield return (DemoRoster.Slp, TeamRole.SpeechLanguagePathologist);
                yield return (DemoRoster.OccupationalTherapist, TeamRole.OccupationalTherapist);
                break;
            case DisabilityCategory.EmotionalDisturbance:
                yield return (DemoRoster.Psychologist, TeamRole.SchoolPsychologist);
                break;
            case DisabilityCategory.IntellectualDisability:
                yield return (DemoRoster.Psychologist, TeamRole.SchoolPsychologist);
                yield return (DemoRoster.Slp, TeamRole.SpeechLanguagePathologist);
                break;
            case DisabilityCategory.MultipleDisabilities:
                yield return (DemoRoster.Slp, TeamRole.SpeechLanguagePathologist);
                yield return (DemoRoster.OccupationalTherapist, TeamRole.OccupationalTherapist);
                yield return (DemoRoster.PhysicalTherapist, TeamRole.PhysicalTherapist);
                break;
            case DisabilityCategory.OrthopedicImpairment:
                yield return (DemoRoster.PhysicalTherapist, TeamRole.PhysicalTherapist);
                yield return (DemoRoster.OccupationalTherapist, TeamRole.OccupationalTherapist);
                break;
            case DisabilityCategory.TraumaticBrainInjury:
                yield return (DemoRoster.OccupationalTherapist, TeamRole.OccupationalTherapist);
                yield return (DemoRoster.Psychologist, TeamRole.SchoolPsychologist);
                break;
            case DisabilityCategory.SpecificLearningDisability:
            case DisabilityCategory.OtherHealthImpairment:
                yield return (DemoRoster.Psychologist, TeamRole.SchoolPsychologist);
                break;
            case null:
                // Still being evaluated — the psychologist leads the assessment work.
                yield return (DemoRoster.Psychologist, TeamRole.SchoolPsychologist);
                yield return (DemoRoster.Slp, TeamRole.SpeechLanguagePathologist);
                break;
            default:
                yield return (DemoRoster.OccupationalTherapist, TeamRole.OccupationalTherapist);
                break;
        }
    }

    private async Task AddTeamMemberAsync(int actingUserId, int studentId, DemoStaff member, TeamRole role,
        List<(string Name, string Role)> teamNames, bool isLead, CancellationToken ct)
    {
        var result = await _studentTeamService.AddMemberAsync(actingUserId, studentId, new AddTeamMemberModel
        {
            StaffProfileId = member.StaffProfileId,
            TeamRole = role,
            IsLead = isLead
        }, ct);

        if (!result.Success)
        {
            if (isLead)
                throw new InvalidOperationException($"Failed to assign case manager for student {studentId}: {result.Message}");
            _logger.LogWarning("Demo seed: could not add {Role} to student {StudentId}: {Message}", role, studentId, result.Message);
            return;
        }

        teamNames.Add((member.FullName, role.ToDisplay()));
    }

    private static DemoStaff Match(List<DemoStaff> staff, DemoRoster.StaffSpec spec) =>
        staff.First(s => s.Email == spec.Email);

    /// <summary>Annual reviews spread from 40 days overdue to 110 days out, so the compliance view has
    /// overdue, due-soon and healthy rows.</summary>
    private static int ReviewOffsetDays(int index, int total) => (index * 150 / Math.Max(1, total - 1)) - 40;

    private static DateTime BirthDateFor(GradeLevel grade, int index, DateTime today) =>
        today.AddYears(-(5 + GradeIndex(grade))).AddDays(-(index * 7 % 300));

    /// <summary>A handful of families speak a language other than English at home — enough that the
    /// interpreter and translated-notice parts of the product have something to show. Indexes into
    /// <see cref="DemoRoster.Students"/>, chosen so the language suits the family's name.</summary>
    private static string HomeLanguageFor(int index) => index switch
    {
        2 or 13 or 26 => "es",  // Diaz, Torres, Castillo
        19 => "hmn",            // Xiong
        _ => "en"
    };

    private static int GradeIndex(GradeLevel grade) => grade switch
    {
        GradeLevel.PK => -1,
        GradeLevel.K => 0,
        GradeLevel.Ungraded => 8,
        _ => int.Parse(grade.ToString().TrimStart('G'))
    };

    // ================================================================================= Documents (IEPs/ETRs) + goals

    internal sealed record DemoDocumentCounts(int FinalizedIeps, int FinalizedEtrs, int DraftIeps);

    private async Task<DemoDocumentCounts> CreateDocumentsAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var iepTypeId = await _context.DocumentTypes.Where(t => t.Key == "IEP").Select(t => t.Id).FirstAsync(ct);
        var etrTypeId = await _context.DocumentTypes.Where(t => t.Key == "ETR").Select(t => t.Id).FirstAsync(ct);

        int finalizedIeps = 0, finalizedEtrs = 0, draftIeps = 0;

        foreach (var student in students)
        {
            switch (student.Track)
            {
                case DemoRoster.Track.IepWithRecentEtr:
                    // Reevaluated first, then this year's IEP written from it — the real order of events.
                    await CreateFinalizedEtrAsync(actingUserId, student, etrTypeId, EvaluationCaseKind.Reevaluation, ct);
                    finalizedEtrs++;
                    await CreateIepAsync(actingUserId, student, iepTypeId, finalize: true, ct);
                    finalizedIeps++;
                    break;

                case DemoRoster.Track.EstablishedIep:
                    await CreateIepAsync(actingUserId, student, iepTypeId, finalize: true, ct);
                    finalizedIeps++;
                    break;

                case DemoRoster.Track.DraftIep:
                    await CreateIepAsync(actingUserId, student, iepTypeId, finalize: false, ct);
                    draftIeps++;
                    break;

                case DemoRoster.Track.EvaluationEtrComplete:
                case DemoRoster.Track.EvaluationInProgress:
                    break; // handled by CreateEvaluationCasesAsync, which owns the whole referral flow
            }

            var done = finalizedIeps + finalizedEtrs + draftIeps;
            if (done > 0 && done % 10 == 0)
                _logger.LogInformation("Demo seed: wrote {Count} documents…", done);
        }

        return new DemoDocumentCounts(finalizedIeps, finalizedEtrs, draftIeps);
    }

    /// <summary>Writes a complete Ohio IEP for the student and either finalizes it (an IEP in force, with
    /// a rendered PDF and projected goal records) or leaves it as the draft a case manager is still
    /// working on.</summary>
    private async Task<int> CreateIepAsync(int actingUserId, DemoStudent student, int iepTypeId, bool finalize, CancellationToken ct)
    {
        var create = await _documentInstanceService.CreateAsync(student.Id, iepTypeId, actingUserId, ct);
        if (!create.Success || create.Data == null)
            throw new InvalidOperationException($"Failed to create IEP draft for student {student.Id}: {create.Message}");

        var form = await LoadFormAsync(create.Data.DocumentTemplateVersionId, ct);
        var today = DateTime.UtcNow.Date;
        var meetingDate = student.AnnualReviewDueDate?.AddYears(-1) ?? today.AddDays(-14);
        if (!finalize)
            meetingDate = student.AnnualReviewDueDate ?? today.AddDays(10); // the meeting this draft is being written for

        var name = student.FirstName;
        var disability = student.Disability;
        var goals = DemoIepContent.GoalsFor(disability, student.Grade, name);

        form.Text(FieldSemantics.StudentProfile,
            $"{student.FullName} — date of birth on file, grade {student.Grade.ToDisplay()} at {student.SchoolName}, {DistrictName}. " +
            $"Primary disability category: {(disability?.ToDisplay() ?? "eligibility determination in progress")}.");

        form.SelectWhereLabel("Meeting type", finalize ? "Annual Review" : "Initial IEP");
        form.Date(FieldSemantics.MeetingDate, meetingDate);
        form.Date(FieldSemantics.EffectiveDates, meetingDate);
        form.DateWhereLabel("IEP effective end date", meetingDate.AddYears(1).AddDays(-1));
        form.DateWhereLabel("Next annual review due", meetingDate.AddYears(1));
        if (student.EtrDate.HasValue)
            form.DateWhereLabel("Next re-evaluation (ETR) due", student.EtrDate.Value.AddYears(3));

        form.Text(FieldSemantics.FuturePlanning,
            $"{name}'s family wants {name} to finish school with the reading, math and self-advocacy skills to take the next step confidently. " +
            (DemoIepContent.IsSecondary(student.Grade)
                ? $"{name} would like to continue into a two-year program after graduation and hold a part-time job while studying."
                : $"{name} enjoys school, and the team wants {name} to keep pace with classmates in the general education classroom with the supports in this plan."));

        form.Text(FieldSemantics.SpecialFactors, SpecialFactorsNarrative(name, disability));
        ApplySpecialFactorChecks(form, disability);

        form.Text(FieldSemantics.PresentLevels, PresentLevelsNarrative(student, goals));
        form.TextWhereLabel("Parent and student concerns",
            $"{name}'s family asks that homework stay manageable at home and that they hear about progress before the next report card rather than at the annual review. " +
            $"{name} says the work is easier when directions are written down as well as spoken.");
        form.Text(FieldSemantics.Eligibility,
            student.EtrDate.HasValue
                ? $"The evaluation team report dated {student.EtrDate.Value:MMMM d, yyyy} found {name} eligible under {(disability?.ToDisplay() ?? "the category under review")}. " +
                  "Progress-monitoring data collected since then is summarised in the present levels above."
                : $"The team reviewed existing evaluation data and classroom progress data for {name}.");

        var esyNeeded = disability is DisabilityCategory.IntellectualDisability or DisabilityCategory.MultipleDisabilities or DisabilityCategory.Autism;
        form.Select(FieldSemantics.ExtendedSchoolYear, esyNeeded ? "Needed" : "Not needed");
        form.TextWhereLabel("ESY rationale",
            esyNeeded
                ? $"Recoupment data from last summer shows {name} loses skills over an extended break and needs more than eight weeks to recover them. Extended school year services will continue the communication and functional goals four mornings a week in July."
                : $"The team reviewed regression and recoupment data and found no evidence that {name} loses critical skills over breaks. Extended school year services are not required at this time.");

        var transition = DemoIepContent.TransitionFor(student.Grade, name);
        if (transition.Count > 0)
        {
            form.TextWhereLabel("Age-appropriate transition assessments",
                $"{name} completed a career interest inventory and a self-determination scale this year. Results point toward hands-on technical fields and show that {name} needs practice asking for accommodations without adult prompting.");
            form.Table(FieldSemantics.Transition, transition.Select(t => new Dictionary<string, string>
            {
                [ColumnSemantics.GoalArea] = t.GoalArea,
                [ColumnSemantics.TransitionServices] = t.Services
            }));
            form.TextWhereLabel("Course of study",
                $"{name} is on track for a diploma, taking the required core courses with support plus the engineering-technology career pathway sequence.");
        }

        form.Table(FieldSemantics.Goals, goals.Select(g => new Dictionary<string, string>
        {
            [ColumnSemantics.Domain] = g.Domain,
            [ColumnSemantics.GoalText] = g.GoalText,
            [ColumnSemantics.Baseline] = g.Baseline,
            [ColumnSemantics.TargetCriteria] = g.TargetCriteria,
            [ColumnSemantics.MeasurementMethod] = g.MeasurementMethod,
            [ColumnSemantics.Timeframe] = g.Timeframe
        }));
        form.Text(FieldSemantics.ProgressMonitoring,
            "Progress toward each annual goal is measured as described in the goal and reported to the family in writing at the end of every grading period, with a copy kept in the student's record.");

        form.Table(FieldSemantics.Services, DemoIepContent.ServicesFor(disability, student.Grade).Select(s => new Dictionary<string, string>
        {
            [ColumnSemantics.ServiceType] = s.ServiceType,
            [ColumnSemantics.Frequency] = s.Frequency,
            [ColumnSemantics.Duration] = s.Duration,
            [ColumnSemantics.Location] = s.Location,
            [ColumnSemantics.ProviderRole] = s.ProviderRole,
            [ColumnSemantics.StartDate] = meetingDate.ToString("yyyy-MM-dd"),
            [ColumnSemantics.EndDate] = meetingDate.AddYears(1).AddDays(-1).ToString("yyyy-MM-dd")
        }));

        form.Table(FieldSemantics.Accommodations, DemoIepContent.AccommodationsFor(disability, student.Grade).Select(a => new Dictionary<string, string>
        {
            [ColumnSemantics.Category] = a.Category,
            [ColumnSemantics.Accommodation] = a.Text
        }));

        form.TextWhereLabel("Modifications",
            disability is DisabilityCategory.IntellectualDisability or DisabilityCategory.MultipleDisabilities
                ? "Grade-level content is modified in depth and breadth, with reduced numbers of items and alternate materials aligned to the extended standards."
                : "No modifications to grade-level content are required; the accommodations above provide access to the general curriculum.");
        form.TextWhereLabel("Support for school personnel",
            "Consultation from the intervention specialist for general education teachers twice per grading period, plus one building in-service on the accommodations in this IEP.");

        var transportationNeeded = disability is DisabilityCategory.OrthopedicImpairment or DisabilityCategory.MultipleDisabilities;
        form.Select(FieldSemantics.Transportation, transportationNeeded ? "Needed" : "Not needed");
        form.TextWhereLabel("Transportation details",
            transportationNeeded
                ? "Wheelchair-accessible bus with a lift, door-to-door service, and an aide on board for the length of the route."
                : "No specialized transportation is required; the student rides the regular bus route.");

        form.TextWhereLabel("Participation in nonacademic and extracurricular activities",
            $"{name} takes part in assemblies, field trips, lunch, recess and club activities with classmates, with the same accommodations that apply during instruction.");
        form.TextWhereLabel("General factors considered",
            $"The team considered {name}'s strengths, the family's concerns, the most recent evaluation results and {name}'s academic, developmental and functional needs in writing this plan.");

        form.Text(FieldSemantics.Lre, LreNarrative(name, disability));
        form.Text(FieldSemantics.Placement,
            disability is DisabilityCategory.IntellectualDisability or DisabilityCategory.MultipleDisabilities
                ? $"{name} is outside the general education setting for approximately 60% of the school day to receive functional academics and related services."
                : $"{name} is outside the general education setting for approximately 15% of the school day, for specially designed instruction and related services.");

        form.Text(FieldSemantics.Testing,
            disability is DisabilityCategory.IntellectualDisability or DisabilityCategory.MultipleDisabilities
                ? "The student participates in the Alternate Assessment for Students with Significant Cognitive Disabilities (AASCD), for which the team confirms the eligibility criteria are met."
                : "The student participates in all statewide and district-wide assessments with the accommodations listed in this IEP: extended time, small-group setting and directions read aloud.");
        form.CheckWhereLabel("Alternate assessment", disability is DisabilityCategory.IntellectualDisability or DisabilityCategory.MultipleDisabilities);

        form.Table(FieldSemantics.Participants, ParticipantRows(student, meetingDate));

        form.Text(FieldSemantics.Signatures,
            "Parent/guardian attended the meeting, received a copy of this IEP and a copy of the procedural safeguards notice, and consented to the services described.");
        form.CheckWhereLabel("Parent received a copy of procedural safeguards", true);
        form.CheckWhereLabel("Parent received a copy of the IEP", true);

        if (form.HasValues)
        {
            var save = await _documentInstanceService.SaveValuesAsync(create.Data.Id, form.Patch, create.Data.RowVersion, actingUserId, ct);
            if (!save.Success)
                throw new InvalidOperationException($"Failed to save IEP values for student {student.Id}: {save.Message}");
        }

        if (!finalize)
            return create.Data.Id;

        var finalized = await _authoredDocumentVersionService.FinalizeAsync(create.Data.Id, actingUserId, ct);
        if (!finalized.Success || finalized.Data == null)
            throw new InvalidOperationException($"Failed to finalize IEP for student {student.Id}: {finalized.Message}");

        await _pdfQueue.EnqueueAsync(finalized.Data.Id, CancellationToken.None);
        await AddGoalObservationsAsync(actingUserId, student, goals, ct);
        return create.Data.Id;
    }

    /// <summary>Writes and finalizes an Ohio ETR. Returns the finalized version id so an evaluation case
    /// can cite it in its determination.</summary>
    private async Task<int> CreateFinalizedEtrAsync(int actingUserId, DemoStudent student, int etrTypeId, EvaluationCaseKind kind, CancellationToken ct)
    {
        var create = await _documentInstanceService.CreateAsync(student.Id, etrTypeId, actingUserId, ct);
        if (!create.Success || create.Data == null)
            throw new InvalidOperationException($"Failed to create ETR draft for student {student.Id}: {create.Message}");

        var form = await LoadFormAsync(create.Data.DocumentTemplateVersionId, ct);
        var etrDate = student.EtrDate ?? DateTime.UtcNow.Date.AddDays(-7);
        var name = student.FirstName;
        var disability = student.Disability;

        form.Text(FieldSemantics.StudentProfile,
            $"{student.FullName} — grade {student.Grade.ToDisplay()} at {student.SchoolName}, {DistrictName}.");
        form.SelectWhereLabel("Evaluation type", kind == EvaluationCaseKind.Reevaluation ? "Reevaluation" : "Initial Evaluation");
        form.DateWhereLabel("Date of referral", etrDate.AddDays(-70));
        form.DateWhereLabel("Date parent consent received", etrDate.AddDays(-58));
        form.Date(FieldSemantics.MeetingDate, etrDate);
        form.DateWhereLabel("Next re-evaluation due", etrDate.AddYears(3));

        form.Text(FieldSemantics.ReferralReason,
            kind == EvaluationCaseKind.Reevaluation
                ? $"{student.FullName} is due for a three-year reevaluation. The team also wanted current data on whether the present services still match {name}'s needs."
                : $"{student.FullName} was referred by the building intervention assistance team after eight weeks of tiered intervention produced limited progress in the areas of concern below.");
        form.Text(FieldSemantics.EvaluationPlan,
            "Areas assessed: cognitive ability, academic achievement, communication, social-emotional and behavioural functioning, and fine-motor skills where indicated. " +
            "Existing data reviewed: intervention progress-monitoring graphs, two years of state and district assessment results, attendance, classroom work samples and parent input gathered by interview.");

        form.Table(FieldSemantics.EvaluatorReports, EvaluatorReportRows(student));

        form.Text(FieldSemantics.TeamSummary,
            $"Taken together, the assessments place {name}'s cognitive ability within the average range with significantly weaker performance in the areas of concern, " +
            "a pattern consistent across the evaluator reports, classroom work samples and progress-monitoring data. Parent and teacher rating scales agree on the areas of need.");
        form.Text(FieldSemantics.PresentLevels,
            $"{name} needs explicit, systematic instruction in the areas identified above, accommodations that reduce the reading and writing load of grade-level tasks, and " +
            "progress monitoring often enough to show whether the instruction is working within a grading period.");
        form.TextWhereLabel("Implications for instruction and progress monitoring",
            "Instruction should be delivered in a small group at least four times a week, with weekly curriculum-based measurement graphed against an aim line and reviewed by the team every six weeks.");

        form.Select(FieldSemantics.EligibilityDetermination, "Eligible");
        if (disability.HasValue)
            form.Select(FieldSemantics.Eligibility, EtrEligibilityOption(disability.Value));
        form.TextWhereLabel("Documentation of eligibility criteria",
            $"The team documents that {name} meets the Ohio Operating Standards criteria for {(disability?.ToDisplay() ?? "the identified category")}: " +
            "the assessment data show the required pattern of need, the condition adversely affects educational performance, and the need for specially designed instruction is established. " +
            "The team also confirmed the determination is not primarily the result of a lack of appropriate instruction or of limited English proficiency.");
        form.CheckWhereLabel("Adverse effect on educational performance", true);
        form.CheckWhereLabel("Need for specially designed instruction", true);
        form.CheckWhereLabel("Determination is not primarily the result", true);

        form.Table(FieldSemantics.Participants, ParticipantRows(student, etrDate));
        form.CheckWhereLabel("Parent received a copy of the ETR", true);
        form.CheckWhereLabel("Parent received a copy of procedural safeguards", true);

        if (form.HasValues)
        {
            var save = await _documentInstanceService.SaveValuesAsync(create.Data.Id, form.Patch, create.Data.RowVersion, actingUserId, ct);
            if (!save.Success)
                throw new InvalidOperationException($"Failed to save ETR values for student {student.Id}: {save.Message}");
        }

        var finalized = await _authoredDocumentVersionService.FinalizeAsync(create.Data.Id, actingUserId, ct);
        if (!finalized.Success || finalized.Data == null)
            throw new InvalidOperationException($"Failed to finalize ETR for student {student.Id}: {finalized.Message}");

        await _pdfQueue.EnqueueAsync(finalized.Data.Id, CancellationToken.None);
        return finalized.Data.Id;
    }

    // --------------------------------------------------------------------- Narrative helpers

    private static string SpecialFactorsNarrative(string name, DisabilityCategory? disability) => disability switch
    {
        DisabilityCategory.EmotionalDisturbance =>
            $"{name}'s behaviour can impede learning. A positive behaviour support plan is in place, with a taught de-escalation routine, a break pass and daily check-in/check-out.",
        DisabilityCategory.Autism =>
            $"{name} has communication needs and benefits from predictability. A visual schedule, advance notice of changes and a quiet break space are used across the school day.",
        DisabilityCategory.HearingImpairment or DisabilityCategory.Deafness =>
            $"{name} is hard of hearing. Personal hearing technology is checked daily, captioned media is used, and staff keep a clear line of sight when speaking.",
        DisabilityCategory.VisualImpairment =>
            $"{name} is visually impaired. Enlarged high-contrast print, screen magnification and a screen reader are available in every setting.",
        DisabilityCategory.MultipleDisabilities =>
            $"{name} has communication needs and uses assistive technology. The communication device travels with {name} and is honoured in every setting; the positioning schedule is followed as written.",
        DisabilityCategory.SpeechOrLanguageImpairment =>
            $"{name} has communication needs addressed through direct speech-language therapy and classroom strategies that give extra time to formulate responses.",
        DisabilityCategory.TraumaticBrainInjury =>
            $"{name} uses assistive technology and written checklists to support memory and organization, with scheduled rest breaks to manage fatigue.",
        _ =>
            $"None of the special instructional factors apply to {name} at this time; the accommodations in this plan address the identified needs."
    };

    private static void ApplySpecialFactorChecks(DemoDocumentForm form, DisabilityCategory? disability)
    {
        form.CheckWhereLabel("Behavior impedes learning", disability is DisabilityCategory.EmotionalDisturbance or DisabilityCategory.Autism);
        form.CheckWhereLabel("Limited English proficiency", false);
        form.CheckWhereLabel("Blind or visually impaired", disability is DisabilityCategory.VisualImpairment);
        form.CheckWhereLabel("Communication needs", disability is DisabilityCategory.SpeechOrLanguageImpairment or DisabilityCategory.Autism
            or DisabilityCategory.MultipleDisabilities or DisabilityCategory.IntellectualDisability);
        form.CheckWhereLabel("Deaf or hard of hearing", disability is DisabilityCategory.HearingImpairment or DisabilityCategory.Deafness);
        form.CheckWhereLabel("Assistive technology", disability is DisabilityCategory.VisualImpairment or DisabilityCategory.OrthopedicImpairment
            or DisabilityCategory.MultipleDisabilities or DisabilityCategory.TraumaticBrainInjury);
    }

    private static string PresentLevelsNarrative(DemoStudent student, IReadOnlyList<DemoIepContent.GoalPlan> goals)
    {
        var name = student.FirstName;
        var areas = string.Join(", ", goals.Select(g => g.Domain.ToLowerInvariant()));
        var baselines = string.Join(" ", goals.Select(g => g.Baseline));
        return $"{name} is a grade {student.Grade.ToDisplay()} student at {student.SchoolName} who is well liked by classmates and works hard when expectations are clear. " +
               $"Current areas of need are {areas}. {baselines} " +
               $"In the general education classroom {name} participates in whole-group instruction and, with the accommodations in this plan, completes the same assignments as classmates in most subjects.";
    }

    private static string LreNarrative(string name, DisabilityCategory? disability) =>
        disability is DisabilityCategory.IntellectualDisability or DisabilityCategory.MultipleDisabilities
            ? $"The team considered full-time general education with supports first. {name} needs functional academics and related services that cannot be delivered in the general education classroom without removing the content's purpose, so {name} receives those in a small group and joins classmates for homeroom, specials, lunch, recess and electives."
            : $"The team considered general education with consultation only, and found {name} needs explicit small-group instruction that cannot be delivered in the general education classroom without reducing instructional time for the class. {name} is therefore served in the resource room for that instruction and remains with classmates for the rest of the school day.";

    /// <summary>Meeting-participant rows: the student's team, the family, and the student themselves once
    /// transition planning applies.</summary>
    private static List<Dictionary<string, string>> ParticipantRows(DemoStudent student, DateTime meetingDate)
    {
        var rows = student.TeamMemberNames
            .Select(m => new Dictionary<string, string>
            {
                [ColumnSemantics.ParticipantName] = m.Name,
                [ColumnSemantics.ParticipantRole] = m.Role
            })
            .ToList();

        var parent = DemoRoster.Parents.FirstOrDefault(p => p.StudentIndex == student.Index);
        if (parent != null)
            rows.Add(new Dictionary<string, string>
            {
                [ColumnSemantics.ParticipantName] = $"{parent.FirstName} {parent.LastName}",
                [ColumnSemantics.ParticipantRole] = "Parent/Guardian"
            });

        if (DemoIepContent.IsSecondary(student.Grade))
            rows.Add(new Dictionary<string, string>
            {
                [ColumnSemantics.ParticipantName] = student.FullName,
                [ColumnSemantics.ParticipantRole] = "Student"
            });

        return rows;
    }

    private static List<Dictionary<string, string>> EvaluatorReportRows(DemoStudent student)
    {
        var name = student.FirstName;
        var psychologist = $"{DemoRoster.Psychologist.FirstName} {DemoRoster.Psychologist.LastName}, School Psychologist";
        var slp = $"{DemoRoster.Slp.FirstName} {DemoRoster.Slp.LastName}, Speech-Language Pathologist";
        var caseManager = $"{student.CaseManager.FullName}, Intervention Specialist";

        var rows = new List<Dictionary<string, string>>
        {
            new()
            {
                [ColumnSemantics.EvaluationDomain] = "Cognitive ability",
                [ColumnSemantics.EvaluatorName] = psychologist,
                [ColumnSemantics.Findings] = $"Standardised cognitive assessment places {name}'s overall ability within the average range, with processing speed and working memory below the other index scores."
            },
            new()
            {
                [ColumnSemantics.EvaluationDomain] = "Academic achievement",
                [ColumnSemantics.EvaluatorName] = caseManager,
                [ColumnSemantics.Findings] = $"Achievement testing and curriculum-based measurement place {name} below grade-level expectations in the areas of concern, with a gap that has widened over two years despite tiered intervention."
            },
            new()
            {
                [ColumnSemantics.EvaluationDomain] = "Observation in the learning environment",
                [ColumnSemantics.EvaluatorName] = psychologist,
                [ColumnSemantics.Findings] = $"Two classroom observations during core instruction show {name} on task for roughly half of the observed intervals and relying on neighbours to start written work."
            }
        };

        if (student.Disability is DisabilityCategory.SpeechOrLanguageImpairment or DisabilityCategory.Autism
            or DisabilityCategory.IntellectualDisability or DisabilityCategory.MultipleDisabilities or null)
            rows.Add(new Dictionary<string, string>
            {
                [ColumnSemantics.EvaluationDomain] = "Communication",
                [ColumnSemantics.EvaluatorName] = slp,
                [ColumnSemantics.Findings] = $"Language testing and a conversational sample show {name}'s receptive language within expectations and expressive language and intelligibility below them."
            });

        return rows;
    }

    private static string EtrEligibilityOption(DisabilityCategory category) => category switch
    {
        // The Ohio ETR form's option list words these two as a single choice.
        DisabilityCategory.HearingImpairment or DisabilityCategory.Deafness => "Deafness (Hearing Impairment)",
        _ => category.ToDisplay()
    };

    // --------------------------------------------------------------------- Goal progress data

    /// <summary>Progress observations against the goals this IEP just projected into goal records: a rising
    /// trend for most students, a flat one for a few, so the progress view has something to interpret.</summary>
    private async Task AddGoalObservationsAsync(int actingUserId, DemoStudent student, IReadOnlyList<DemoIepContent.GoalPlan> plans, CancellationToken ct)
    {
        var goals = await _goalRecordService.GetForStudentAsync(actingUserId, student.Id, ct);
        if (!goals.Success || goals.Data == null)
            return;

        var random = new Random(student.Id); // deterministic per student
        var stalled = student.Index % 7 == 3; // roughly one student in seven is not making progress

        foreach (var goal in goals.Data)
        {
            var unit = plans.FirstOrDefault(p => p.Domain == goal.Domain)?.Unit ?? "% accuracy";
            var observationCount = 4 + random.Next(3); // 4-6 observations, fortnightly
            var start = 45 + random.Next(20);
            for (var i = 0; i < observationCount; i++)
            {
                var growth = stalled ? random.Next(-2, 3) : (i * (4 + random.Next(4)));
                await _goalRecordService.AddObservationAsync(actingUserId, goal.Id, new CreateGoalObservationModel
                {
                    ObservedAt = DateTime.UtcNow.AddDays(-((observationCount - i) * 14)),
                    Value = Math.Clamp(start + growth, 0, 100),
                    Unit = unit,
                    Note = i == observationCount - 1 && stalled
                        ? "Progress-monitoring probe — trend is flat; team to review the intervention."
                        : "Fortnightly progress-monitoring probe."
                }, ct);
            }
        }
    }

    // --------------------------------------------------------------------- Template form helper

    /// <summary>
    /// A pinned template version's fields, addressable by semantic tag (the same mechanism
    /// <c>GoalRecordService.ProjectOnFinalizeAsync</c> uses to find "the Goals table") or, for the fields an
    /// Ohio form carries without one, by a distinctive fragment of their label. Every setter is a no-op when
    /// the template has no such field, so the seeder never depends on one particular template revision.
    /// </summary>
    private sealed class DemoDocumentForm
    {
        private readonly IReadOnlyDictionary<string, SemanticField> _semantics;
        private readonly IReadOnlyList<(Guid Key, FieldType Type, string Label)> _fields;
        private readonly Dictionary<string, JsonElement> _patch = new();
        private readonly ILogger _logger;

        public DemoDocumentForm(IReadOnlyDictionary<string, SemanticField> semantics,
            IReadOnlyList<(Guid Key, FieldType Type, string Label)> fields, ILogger logger)
        {
            _semantics = semantics;
            _fields = fields;
            _logger = logger;
        }

        public IReadOnlyDictionary<string, JsonElement> Patch => _patch;
        public bool HasValues => _patch.Count > 0;

        public void Text(string semantic, string value) => SetBySemantic(semantic, value, FieldType.Text, FieldType.RichText);

        public void Select(string semantic, string option) => SetBySemantic(semantic, option, FieldType.Select);

        public void Date(string semantic, DateTime value) => SetBySemantic(semantic, value.ToString("yyyy-MM-dd"), FieldType.Date);

        public void Table(string semantic, IEnumerable<IReadOnlyDictionary<string, string>> rows)
        {
            if (!_semantics.TryGetValue(semantic, out var field) || field.FieldType != FieldType.Table)
                return;

            var mapped = new List<Dictionary<string, string>>();
            foreach (var row in rows)
            {
                var cells = new Dictionary<string, string>();
                foreach (var (columnSemantic, value) in row)
                    if (field.Columns.TryGetValue(columnSemantic, out var columnKey))
                        cells[columnKey.ToString()] = value;
                if (cells.Count > 0)
                    mapped.Add(cells);
            }

            if (mapped.Count > 0)
                _patch[field.FieldKey.ToString()] = JsonSerializer.SerializeToElement(mapped);
        }

        public void TextWhereLabel(string labelFragment, string value) => SetByLabel(labelFragment, JsonSerializer.SerializeToElement(value), FieldType.Text, FieldType.RichText);

        public void SelectWhereLabel(string labelFragment, string option) => SetByLabel(labelFragment, JsonSerializer.SerializeToElement(option), FieldType.Select);

        public void DateWhereLabel(string labelFragment, DateTime value) => SetByLabel(labelFragment, JsonSerializer.SerializeToElement(value.ToString("yyyy-MM-dd")), FieldType.Date);

        public void CheckWhereLabel(string labelFragment, bool value) => SetByLabel(labelFragment, JsonSerializer.SerializeToElement(value), FieldType.Checkbox);

        private void SetBySemantic(string semantic, string value, params FieldType[] allowed)
        {
            if (!_semantics.TryGetValue(semantic, out var field) || !allowed.Contains(field.FieldType))
                return;
            _patch[field.FieldKey.ToString()] = JsonSerializer.SerializeToElement(value);
        }

        /// <summary>Sets the single field whose label contains <paramref name="labelFragment"/>. A fragment
        /// that matches none, or more than one, is skipped and logged — the demo document simply leaves that
        /// field blank rather than guessing.</summary>
        private void SetByLabel(string labelFragment, JsonElement value, params FieldType[] allowed)
        {
            var matches = _fields
                .Where(f => allowed.Contains(f.Type) && f.Label.Contains(labelFragment, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                _logger.LogDebug("Demo seed: template has {Count} fields matching '{Fragment}' — leaving it blank.", matches.Count, labelFragment);
                return;
            }
            _patch[matches[0].Key.ToString()] = value;
        }
    }

    private async Task<DemoDocumentForm> LoadFormAsync(int templateVersionId, CancellationToken ct)
    {
        var sections = await _context.TemplateSections
            .Where(s => s.DocumentTemplateVersionId == templateVersionId)
            .Include(s => s.Fields)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        var fields = sections
            .OrderBy(s => s.DisplayOrder)
            .SelectMany(s => s.Fields.OrderBy(f => f.DisplayOrder))
            .Select(f => (f.FieldKey, f.FieldType, f.Label))
            .ToList();

        return new DemoDocumentForm(TemplateSemanticsReader.Read(sections), fields, _logger);
    }

    // ================================================================================= Evaluation cases

    internal sealed record DemoEvaluationCounts(int Cases, int FinalizedEtrs, int DraftIeps);

    /// <summary>
    /// The referral side of the district: initial evaluations at every stage of the 60-day clock, and the
    /// closed reevaluation cases behind the students whose ETR was just completed. Each one is driven
    /// through the real case service in the order a district works it — referral, consent requested,
    /// consent received, evaluator assignments, ETR, determination, IEP handoff.
    /// </summary>
    private async Task<DemoEvaluationCounts> CreateEvaluationCasesAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var etrTypeId = await _context.DocumentTypes.Where(t => t.Key == "ETR").Select(t => t.Id).FirstAsync(ct);
        int cases = 0, etrs = 0, draftIeps = 0;

        // ---- Initial evaluations that finished: consent, assessments, ETR, eligibility, first IEP ----
        foreach (var student in students.Where(s => s.Track == DemoRoster.Track.EvaluationEtrComplete))
        {
            var etrDate = student.EtrDate ?? today.AddDays(-7);
            if (!await OpenCaseAsync(actingUserId, student, EvaluationCaseKind.Initial, etrDate.AddDays(-70),
                    $"Referred by the building intervention assistance team after tiered intervention produced limited progress.", ct))
                continue;
            cases++;

            await _evaluationCaseService.RequestConsentAsync(actingUserId, student.Id, etrDate.AddDays(-64), ct);
            await _evaluationCaseService.ReceiveConsentAsync(actingUserId, student.Id, new ReceiveConsentModel { ReceivedAt = etrDate.AddDays(-58) }, ct);
            await AddEvaluatorAssignmentsAsync(actingUserId, student, etrDate.AddDays(-14), submittedOn: etrDate.AddDays(-16), ct);

            var etrVersionId = await CreateFinalizedEtrAsync(actingUserId, student, etrTypeId, EvaluationCaseKind.Initial, ct);
            etrs++;

            var determine = await _evaluationCaseService.DetermineAsync(actingUserId, student.Id, new DetermineEvaluationModel
            {
                Outcome = EligibilityOutcome.Eligible,
                DeterminationDate = etrDate,
                Rationale = $"The team found {student.FirstName} eligible under {(student.Disability?.ToDisplay() ?? "the identified category")}: " +
                            "the assessment data show the required pattern of need, the condition adversely affects educational performance, and specially designed instruction is required.",
                EtrAuthoredVersionId = etrVersionId
            }, ct);
            if (!determine.Success)
                _logger.LogWarning("Demo seed: could not record determination for student {StudentId}: {Message}", student.Id, determine.Message);

            // The first IEP, started from the ETR the team just wrote and still in progress.
            var iep = await _evaluationCaseService.CreateIepFromEtrAsync(actingUserId, student.Id, ct);
            if (iep.Success)
                draftIeps++;
            else
                _logger.LogWarning("Demo seed: could not start the IEP from the ETR for student {StudentId}: {Message}", student.Id, iep.Message);
        }

        // ---- Initial evaluations still running: one awaiting consent, one mid-assessment ----
        var inProgress = students.Where(s => s.Track == DemoRoster.Track.EvaluationInProgress).ToList();
        for (var i = 0; i < inProgress.Count; i++)
        {
            var student = inProgress[i];
            var awaitingConsent = i % 2 == 0;
            var referralDate = awaitingConsent ? today.AddDays(-9) : today.AddDays(-38);

            if (!await OpenCaseAsync(actingUserId, student, EvaluationCaseKind.Initial, referralDate,
                    awaitingConsent
                        ? "Parent request for an initial evaluation, received in writing."
                        : "Referred by the classroom teacher after six weeks of Tier 3 intervention with limited response.", ct))
                continue;
            cases++;

            await _evaluationCaseService.RequestConsentAsync(actingUserId, student.Id, referralDate.AddDays(2), ct);
            if (awaitingConsent)
                continue; // consent has not come back yet — the state a district chases

            await _evaluationCaseService.ReceiveConsentAsync(actingUserId, student.Id, new ReceiveConsentModel { ReceivedAt = referralDate.AddDays(8) }, ct);
            // One assessment in, one already past its internal due date — the evaluator-overdue case.
            await AddEvaluatorAssignmentsAsync(actingUserId, student, today.AddDays(-4), submittedOn: null, ct);
        }

        // ---- Reevaluations that closed out behind this year's IEP ----
        foreach (var student in students.Where(s => s.Track == DemoRoster.Track.IepWithRecentEtr))
        {
            var etrDate = student.EtrDate ?? today.AddDays(-21);
            if (!await OpenCaseAsync(actingUserId, student, EvaluationCaseKind.Reevaluation, etrDate.AddDays(-70),
                    "Three-year reevaluation due; the team also wanted current data on whether the present services still match the student's needs.", ct))
                continue;
            cases++;

            await _evaluationCaseService.RequestConsentAsync(actingUserId, student.Id, etrDate.AddDays(-64), ct);
            await _evaluationCaseService.ReceiveConsentAsync(actingUserId, student.Id, new ReceiveConsentModel { ReceivedAt = etrDate.AddDays(-58) }, ct);
            await AddEvaluatorAssignmentsAsync(actingUserId, student, etrDate.AddDays(-14), submittedOn: etrDate.AddDays(-17), ct);

            var etrVersionId = await _context.AuthoredDocumentVersions.AsNoTracking()
                .Where(v => v.SchoolStudentId == student.Id && v.DocumentType.Key == "ETR")
                .OrderByDescending(v => v.Id)
                .Select(v => (int?)v.Id)
                .FirstOrDefaultAsync(ct);

            await _evaluationCaseService.DetermineAsync(actingUserId, student.Id, new DetermineEvaluationModel
            {
                Outcome = EligibilityOutcome.Eligible,
                DeterminationDate = etrDate,
                Rationale = $"{student.FirstName} continues to meet the eligibility criteria for {(student.Disability?.ToDisplay() ?? "the identified category")} and continues to require specially designed instruction.",
                EtrAuthoredVersionId = etrVersionId
            }, ct);
            await _evaluationCaseService.CloseAsync(actingUserId, student.Id, ct);
        }

        return new DemoEvaluationCounts(cases, etrs, draftIeps);
    }

    private async Task<bool> OpenCaseAsync(int actingUserId, DemoStudent student, EvaluationCaseKind kind, DateTime referralDate, string referralSource, CancellationToken ct)
    {
        var result = await _evaluationCaseService.CreateAsync(actingUserId, student.Id, new CreateEvaluationCaseModel
        {
            Kind = kind,
            ReferralDate = referralDate,
            ReferralSource = referralSource
        }, ct);

        if (result.Success)
            return true;

        _logger.LogWarning("Demo seed: could not open an evaluation case for student {StudentId}: {Message}", student.Id, result.Message);
        return false;
    }

    /// <summary>One assignment per evaluator on the student's team. <paramref name="submittedOn"/> null
    /// leaves them outstanding (so a case shows work still owed, and an overdue one once the due date has
    /// passed); a date marks every assignment but the last as submitted.</summary>
    private async Task AddEvaluatorAssignmentsAsync(int actingUserId, DemoStudent student, DateTime dueDate, DateTime? submittedOn, CancellationToken ct)
    {
        var assignments = new List<(int UserId, string Domain)>
        {
            (student.Evaluator?.UserId ?? student.CaseManager.UserId, "Cognitive ability and observation"),
            (student.CaseManager.UserId, "Academic achievement")
        };

        for (var i = 0; i < assignments.Count; i++)
        {
            var (userId, domain) = assignments[i];
            var created = await _evaluationCaseService.AddAssignmentAsync(actingUserId, student.Id, new CreateEvaluatorAssignmentModel
            {
                UserId = userId,
                Domain = domain,
                DueDate = dueDate
            }, ct);

            if (!created.Success || created.Data == null)
            {
                _logger.LogWarning("Demo seed: could not add the '{Domain}' assignment for student {StudentId}: {Message}", domain, student.Id, created.Message);
                continue;
            }

            // The last assignment stays outstanding when nothing has been submitted yet.
            var submit = submittedOn ?? (i < assignments.Count - 1 ? dueDate.AddDays(-2) : (DateTime?)null);
            if (submit == null)
                continue;

            await _evaluationCaseService.UpdateAssignmentAsync(actingUserId, created.Data.Id, new UpdateEvaluatorAssignmentModel
            {
                SubmittedAt = submit,
                Notes = "Report uploaded to the shared evaluation folder and summarised in the ETR."
            }, ct);
        }
    }

    // ================================================================================= Meetings

    /// <summary>
    /// Meetings a district would have on its calendar right now: four already held (with the decisions the
    /// team recorded and attendance taken), eight scheduled over the next month, and two still proposed
    /// while the family settles on a time. Each is created by the student's own case manager, so the
    /// "my meetings" view is populated for the staff logins as well as the admin's.
    /// </summary>
    private async Task<int> CreateMeetingsAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var withIep = students.Where(s => s.GetsFinalizedIep).ToList();
        if (withIep.Count == 0)
            return 0;

        var today = DateTime.UtcNow.Date;
        var created = 0;

        // ---- Held, with decisions ----
        var heldPlan = new (int Offset, MeetingType Type)[]
        {
            (-45, MeetingType.AnnualReview),
            (-31, MeetingType.Amendment),
            (-18, MeetingType.AnnualReview),
            (-9, MeetingType.Transition)
        };
        for (var i = 0; i < heldPlan.Length && i < withIep.Count; i++)
        {
            var (offset, type) = heldPlan[i];
            var student = PickMeetingStudent(withIep);
            var meeting = await ScheduleMeetingAsync(student, type, today.AddDays(offset).AddHours(14), "Demo seed data.", ct);
            if (meeting == null)
                continue;
            created++;

            var held = await _meetingService.SetStatusAsync(student.CaseManager.UserId, meeting.Id, MeetingStatus.Held, ct);
            if (!held.Success || held.Data == null)
            {
                _logger.LogWarning("Demo seed: could not mark meeting {MeetingId} as held: {Message}", meeting.Id, held.Message);
                continue;
            }

            await RecordFullAttendanceAsync(student, held.Data, ct);
            foreach (var (text, outcome) in DecisionsFor(student, type))
                await _meetingDecisionService.CreateAsync(student.CaseManager.UserId, meeting.Id, new CreateMeetingDecisionModel
                {
                    Text = text,
                    Outcome = outcome
                }, ct);
        }

        // ---- Scheduled over the next month ----
        // Each type is drawn from a pool it makes sense for: an eligibility meeting for a student who is
        // actually being evaluated, an initial IEP for one who was just found eligible, a transition
        // meeting for a high-schooler.
        var beingEvaluated = students.Where(s => s.Track is DemoRoster.Track.EvaluationInProgress or DemoRoster.Track.EvaluationEtrComplete).ToList();
        var newlyEligible = students.Where(s => s.Track is DemoRoster.Track.DraftIep or DemoRoster.Track.EvaluationEtrComplete).ToList();
        var secondary = students.Where(s => s.GetsFinalizedIep && DemoIepContent.IsSecondary(s.Grade)).ToList();

        var upcomingPlan = new (int Offset, MeetingType Type, List<DemoStudent> Pool)[]
        {
            (3, MeetingType.AnnualReview, withIep),
            (6, MeetingType.EtrEligibility, beingEvaluated.Count > 0 ? beingEvaluated : withIep),
            (9, MeetingType.AnnualReview, withIep),
            (13, MeetingType.Amendment, withIep),
            (17, MeetingType.InitialIep, newlyEligible.Count > 0 ? newlyEligible : withIep),
            (20, MeetingType.AnnualReview, withIep),
            (24, MeetingType.Reevaluation, withIep),
            (28, MeetingType.Transition, secondary.Count > 0 ? secondary : withIep)
        };
        for (var i = 0; i < upcomingPlan.Length; i++)
        {
            var (offset, type, pool) = upcomingPlan[i];
            var student = PickMeetingStudent(pool);
            var meeting = await ScheduleMeetingAsync(student, type, today.AddDays(offset).AddHours(i % 2 == 0 ? 14 : 9), null, ct);
            if (meeting == null)
                continue;
            created++;
            await RecordRsvpsAsync(meeting, familyReplies: i != 3, ct);
        }

        // ---- Further out, still waiting on the family to settle on a time ----
        // These stay Scheduled: MeetingService.SetStatusAsync accepts only Scheduled/Held/Continued, so
        // MeetingStatus.Proposed is not reachable through the real service and the seeder does not fake it.
        // The note carries the "waiting on the family" state instead.
        for (var i = 0; i < 2; i++)
        {
            var student = PickMeetingStudent(withIep);
            var meeting = await ScheduleMeetingAsync(student, MeetingType.AnnualReview, today.AddDays(34 + i * 4).AddHours(15),
                "Three times offered to the family; waiting on a reply before this date is confirmed.", ct);
            if (meeting != null)
                created++;
        }

        return created;
    }

    /// <summary>Students already given a meeting, so no student ends up with three of them.</summary>
    private readonly HashSet<int> _studentsWithAMeeting = new();

    /// <summary>
    /// Picks who this meeting is for. Students whose family is linked come first, so the participant list
    /// a meeting builds by default actually shows the parent (and, for a high-schooler with an account, the
    /// student) the way a real IEP meeting invitation does. After that it spreads deterministically across
    /// buildings and case managers, and never gives the same student two meetings while others have none.
    /// </summary>
    private DemoStudent PickMeetingStudent(List<DemoStudent> pool)
    {
        var ordered = pool
            .OrderByDescending(HasLinkedFamily)
            .ThenBy(s => (s.Index * 13 + 7) % 41)
            .ToList();

        var pick = ordered.FirstOrDefault(s => !_studentsWithAMeeting.Contains(s.Id)) ?? ordered[0];
        _studentsWithAMeeting.Add(pick.Id);
        return pick;
    }

    private static bool HasLinkedFamily(DemoStudent student) =>
        DemoRoster.Parents.Any(p => p.StudentIndex == student.Index && p.Engagement != DemoRoster.FamilyEngagement.InvitePending);

    private async Task<MeetingModel?> ScheduleMeetingAsync(DemoStudent student, MeetingType type, DateTime startsAtUtc, string? notes, CancellationToken ct)
    {
        var result = await _meetingService.CreateAsync(student.CaseManager.UserId, student.Id, new CreateMeetingModel
        {
            Type = type,
            Title = $"{type.ToDisplay()} — {student.FullName}",
            StartsAtUtc = startsAtUtc,
            DurationMinutes = type == MeetingType.Amendment ? 30 : 60,
            Location = $"{student.SchoolName} conference room",
            Notes = notes
        }, ct);

        if (result.Success && result.Data != null)
            return result.Data;

        _logger.LogWarning("Demo seed: failed to create a {Type} meeting for student {StudentId}: {Message}", type, student.Id, result.Message);
        return null;
    }

    /// <summary>
    /// Everyone attended, except — on a roster large enough for it — one related-service provider excused
    /// under IDEA's written-agreement rule, having sent written input instead. Never the family or the
    /// student: an excused parent is the wrong thing to show in a demo, and it is not the case the excusal
    /// rule exists for.
    /// </summary>
    private async Task RecordFullAttendanceAsync(DemoStudent student, MeetingModel meeting, CancellationToken ct)
    {
        if (meeting.Participants == null || meeting.Participants.Count == 0)
            return;

        var excusable = meeting.Participants.LastOrDefault(p =>
            !p.IsFamily && !p.IsStudent &&
            p.TeamRole is TeamRole.SpeechLanguagePathologist or TeamRole.OccupationalTherapist
                or TeamRole.PhysicalTherapist or TeamRole.SchoolPsychologist);
        var excusedParticipantId = meeting.Participants.Count > 4 ? excusable?.Id : null;

        var items = meeting.Participants.Select(p => new AttendanceItemModel
        {
            ParticipantId = p.Id,
            Attended = p.Id != excusedParticipantId,
            ExcusalNote = p.Id == excusedParticipantId
                ? "Excused by written agreement with the parent; written input was provided to the team instead."
                : null
        }).ToList();

        var result = await _meetingService.RecordAttendanceAsync(student.CaseManager.UserId, meeting.Id, items, ct);
        if (!result.Success)
            _logger.LogWarning("Demo seed: could not record attendance for meeting {MeetingId}: {Message}", meeting.Id, result.Message);
    }

    /// <summary>
    /// RSVPs on an upcoming meeting, each submitted by the participant themselves (the service only lets a
    /// caller answer for their own row). The team accepts; the family accepts on all but one meeting, which
    /// is left unanswered so the "no reply yet" state has an example.
    /// </summary>
    private async Task RecordRsvpsAsync(MeetingModel meeting, bool familyReplies, CancellationToken ct)
    {
        if (meeting.Participants == null)
            return;

        foreach (var participant in meeting.Participants.Where(p => p.UserId != null))
        {
            if ((participant.IsFamily || participant.IsStudent) && !familyReplies)
                continue;

            var status = participant.TeamRole == TeamRole.LeaRepresentative ? InviteStatus.Tentative : InviteStatus.Accepted;
            var result = await _meetingService.RsvpAsync(participant.UserId!.Value, meeting.Id, status, ct);
            if (!result.Success)
                _logger.LogWarning("Demo seed: could not record an RSVP on meeting {MeetingId}: {Message}", meeting.Id, result.Message);
        }
    }

    private static IEnumerable<(string Text, MeetingDecisionOutcome Outcome)> DecisionsFor(DemoStudent student, MeetingType type)
    {
        var name = student.FirstName;
        switch (type)
        {
            case MeetingType.Amendment:
                yield return ($"Team agreed to add 30 minutes of specialized reading instruction four times a week for {name}.", MeetingDecisionOutcome.Agreed);
                yield return ("Family asked for the amended IEP in writing before it takes effect; the case manager will send it this week.", MeetingDecisionOutcome.Agreed);
                break;
            case MeetingType.Transition:
                yield return ($"Team agreed to refer {name} to Opportunities for Ohioans with Disabilities before the next annual review.", MeetingDecisionOutcome.Agreed);
                yield return ("Decision on a shortened school day deferred until the team reviews attendance data in six weeks.", MeetingDecisionOutcome.Deferred);
                break;
            default:
                yield return ($"Team agreed to continue the current services and goals for {name}, with the reading goal raised to a 95% accuracy criterion.", MeetingDecisionOutcome.Agreed);
                yield return ("Family requested additional math progress data before agreeing to reduce resource-room time.", MeetingDecisionOutcome.Deferred);
                yield return ("Team did not agree on extended school year services; the family's written objection is attached to the record.", MeetingDecisionOutcome.Disagreed);
                break;
        }
    }

    // ================================================================================= Family engagement

    /// <summary>Parent user id of each engaged family, by student index — set by
    /// <see cref="LinkFamiliesAsync"/> and read by <see cref="ShareDraftsWithFamiliesAsync"/> once the
    /// documents those families are asked to review exist.</summary>
    private readonly Dictionary<int, int> _engagedParentUserIdByStudentIndex = new();

    private async Task LinkFamiliesAsync(List<DemoStudent> students, List<DemoLoginRow> logins, CancellationToken ct)
    {
        foreach (var parent in DemoRoster.Parents)
        {
            var student = students[parent.StudentIndex];
            var caseManagerUserId = student.CaseManager.UserId;

            var invite = await _childLinkService.InviteParentAsync(caseManagerUserId, student.Id, parent.Email, ct);
            if (!invite.Success)
            {
                _logger.LogWarning("Demo seed: could not invite parent {Email}: {Message}", parent.Email, invite.Message);
                continue;
            }

            if (parent.Engagement == DemoRoster.FamilyEngagement.InvitePending)
            {
                // Deliberately left unaccepted: the pending-invite row a district follows up on. No account
                // exists for this address, so it is not a login.
                logins.Add(new DemoLoginRow($"Parent of {student.FullName} — INVITE PENDING, cannot log in", parent.Email, "(invite not accepted)"));
                continue;
            }

            var parentUserId = await CreateAccountUserAsync(parent.Email, parent.FirstName, parent.LastName, ct);
            var token = await ExtractLatestTokenAsync(parent.Email, "SchoolLinkInvite", ct);
            var accept = await _childLinkService.AcceptInviteAsync(parentUserId, token, null, ct);
            if (!accept.Success)
                throw new InvalidOperationException($"Failed to accept the parent link for '{parent.Email}': {accept.Message}");

            var label = parent.Engagement == DemoRoster.FamilyEngagement.ReviewedDraft
                ? $"Parent of {student.FullName} (sent a draft, has replied)"
                : $"Parent of {student.FullName}";
            logins.Add(new DemoLoginRow(label, parent.Email, DemoPassword));

            if (parent.Engagement == DemoRoster.FamilyEngagement.ReviewedDraft)
                _engagedParentUserIdByStudentIndex[parent.StudentIndex] = parentUserId;
        }

        await CreateStudentAccountAsync(students, logins, ct);
    }

    private async Task ShareDraftsWithFamiliesAsync(List<DemoStudent> students, CancellationToken ct)
    {
        foreach (var (studentIndex, parentUserId) in _engagedParentUserIdByStudentIndex)
            await ShareDraftWithParentAsync(students[studentIndex], parentUserId, ct);
    }

    /// <summary>Sends the student's IEP to the family and records the conversation that came back: two
    /// questions, an agreement, and the family's acknowledgement that they have read it.</summary>
    private async Task ShareDraftWithParentAsync(DemoStudent student, int parentUserId, CancellationToken ct)
    {
        // The Draft instance stays Status=Draft after finalize (AuthoredDocumentVersionService re-opens it),
        // so sharing it shares the finalized values the family would actually see.
        var instanceId = await _context.DocumentInstances
            .Where(d => d.SchoolStudentId == student.Id)
            .OrderByDescending(d => d.Id)
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync(ct);
        if (instanceId == null)
        {
            _logger.LogWarning("Demo seed: student {StudentId} has no document to share with the family.", student.Id);
            return;
        }

        var share = await _draftSharingService.ShareAsync(student.CaseManager.UserId, instanceId.Value,
            $"Here is the draft IEP ahead of {student.FirstName}'s meeting. Please read the goals and services sections and tell us what you think — questions are welcome.", ct);
        if (!share.Success || share.Data == null)
        {
            _logger.LogWarning("Demo seed: failed to share the draft for student {StudentId}: {Message}", student.Id, share.Message);
            return;
        }

        await _draftResponseService.CreateAsync(parentUserId, share.Data.Id, new CreateDraftResponseModel
        {
            Kind = DraftResponseKind.Question,
            Text = "Can you say more about how the reading goal will be measured, and how often we will hear about progress?"
        }, ct);
        await _draftResponseService.CreateAsync(parentUserId, share.Data.Id, new CreateDraftResponseModel
        {
            Kind = DraftResponseKind.Question,
            Text = $"Who will be delivering the specialized instruction, and will {student.FirstName} miss any part of the general education class to attend?"
        }, ct);
        await _draftResponseService.CreateAsync(parentUserId, share.Data.Id, new CreateDraftResponseModel
        {
            Kind = DraftResponseKind.Agree,
            Text = "The math goal and the accommodations look right to us — thank you for writing them so clearly."
        }, ct);
        await _draftSharingService.AcknowledgeAsync(parentUserId, share.Data.Id, ct);
    }

    /// <summary>One high-school student with their own account, invited by their case manager, so the
    /// student view of transition planning can be demonstrated.</summary>
    private async Task CreateStudentAccountAsync(List<DemoStudent> students, List<DemoLoginRow> logins, CancellationToken ct)
    {
        var student = students[DemoRoster.StudentAccountStudentIndex];
        var email = DemoRoster.StudentAccountEmail;
        var studentUserId = await CreateAccountUserAsync(email, student.FirstName, student.LastName, ct);

        var invite = await _studentInviteService.InviteFromEducatorAsync(student.CaseManager.UserId, student.Id, email, ct);
        if (!invite.Success)
            throw new InvalidOperationException($"Failed to invite the student account: {invite.Message}");

        var token = await ExtractLatestTokenAsync(email, "StudentInvite", ct);
        var accept = await _studentInviteService.AcceptInviteAsync(studentUserId, token, consentAccepted: true, ct);
        if (!accept.Success)
            throw new InvalidOperationException($"Failed to accept the student invite: {accept.Message}");

        logins.Add(new DemoLoginRow($"Student — {student.FullName}, grade {student.Grade.ToDisplay()}", email, DemoPassword));
    }

    /// <summary>
    /// Creates a Parent-role account the "real" way: <see cref="IAuthService.RegisterAsync"/> requires a
    /// valid <see cref="BetaInviteCode"/> (closed-beta gate) — inserting one directly is the pragmatic,
    /// narrowly-scoped exception (beta-program infrastructure, not application/student data) needed to
    /// drive the real registration service instead of inserting a User row by hand.
    /// </summary>
    private async Task<int> CreateAccountUserAsync(string email, string firstName, string lastName, CancellationToken ct)
    {
        var code = $"DEMO-{Guid.NewGuid():N}"[..20]; // BetaInviteCode.Code is HasMaxLength(20)
        _context.Set<BetaInviteCode>().Add(new BetaInviteCode { Code = code, IsActive = true });
        await _context.SaveChangesAsync(ct);

        var register = await _authService.RegisterAsync(new RegisterModel
        {
            Email = email,
            Password = DemoPassword,
            FirstName = firstName,
            LastName = lastName,
            InviteCode = code
        }, ct);
        if (!register.Success)
            throw new InvalidOperationException($"Failed to register demo account '{email}': {register.Message}");

        return await _context.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync(ct);
    }

    // ================================================================================= Contact attempts

    /// <summary>The documented outreach behind the meetings: the record a district needs when a family does
    /// not respond. Recorded by the case manager who made the call.</summary>
    private async Task CreateContactAttemptsAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var attempts = new (int Seed, int DaysAgo, FamilyContactMethod Method, FamilyContactOutcome Outcome, string Note)[]
        {
            (0, 3, FamilyContactMethod.Phone, FamilyContactOutcome.Reached, "Called to confirm the annual review date and time; family confirmed."),
            (5, 6, FamilyContactMethod.Email, FamilyContactOutcome.Reached, "Emailed the draft IEP and the procedural safeguards notice."),
            (9, 8, FamilyContactMethod.Phone, FamilyContactOutcome.LeftMessage, "First attempt to schedule the annual review; left a voicemail."),
            (9, 5, FamilyContactMethod.Phone, FamilyContactOutcome.NoAnswer, "Second attempt to schedule; no answer, no voicemail available."),
            (9, 2, FamilyContactMethod.Letter, FamilyContactOutcome.LeftMessage, "Third attempt: written notice of the proposed meeting date mailed home."),
            (14, 11, FamilyContactMethod.InPerson, FamilyContactOutcome.Reached, "Spoke with the parent at pick-up; agreed on a morning meeting."),
            (19, 4, FamilyContactMethod.Phone, FamilyContactOutcome.Reached, "Discussed the progress-monitoring data ahead of the meeting."),
            (23, 13, FamilyContactMethod.Email, FamilyContactOutcome.NoAnswer, "Emailed three proposed meeting times; no reply yet."),
            (28, 7, FamilyContactMethod.Phone, FamilyContactOutcome.Reached, "Interpreter arranged for the meeting at the family's request."),
            (33, 9, FamilyContactMethod.Phone, FamilyContactOutcome.LeftMessage, "Left a message about the evaluation consent form.")
        };

        foreach (var (seed, daysAgo, method, outcome, note) in attempts)
        {
            var student = students[seed % students.Count];
            var result = await _familyContactService.RecordContactAttemptAsync(student.CaseManager.UserId, student.Id, new CreateFamilyContactAttemptModel
            {
                AttemptedAt = DateTime.UtcNow.AddDays(-daysAgo),
                Method = method,
                Outcome = outcome,
                Note = note
            }, ct);

            if (!result.Success)
                _logger.LogWarning("Demo seed: failed to record a contact attempt for student {StudentId}: {Message}", student.Id, result.Message);
        }
    }
    // ================================================================================= ResetAsync

    /// <summary>The four SQL Server INSTEAD OF UPDATE/DELETE triggers from migration
    /// <c>AddPilotGatesPhase12</c> — see that migration for the exact trigger bodies. Only two of these
    /// tables (AuthoredDocumentVersions, SharedDraftRevisions) actually hold demo rows the reset deletes;
    /// all four are disabled/re-enabled regardless, matching the plan-8 contract's instruction literally
    /// and staying correct if a future seeder change starts touching AccessAuditLogs/IepVersions too.</summary>
    private static readonly (string Trigger, string Table)[] ImmutabilityTriggers =
    {
        ("TR_AccessAuditLogs_Immutable", "AccessAuditLogs"),
        ("TR_AuthoredDocumentVersions_Immutable", "AuthoredDocumentVersions"),
        ("TR_IepVersions_Immutable", "IepVersions"),
        ("TR_SharedDraftRevisions_Immutable", "SharedDraftRevisions")
    };

    public async Task<DemoSeedResult> ResetAsync(CancellationToken ct = default)
    {
        // A reset touches ~20+ tables inside one transaction; the default 30s ADO.NET command timeout
        // has been observed to trip under transient contention on a shared QA instance even though no
        // single delete is intrinsically slow. This is a deliberate, generous ceiling for an
        // infrequent admin operation, not a tuning of the app's normal request-path timeout.
        _context.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));

        var district = await _context.Districts.FirstOrDefaultAsync(d => d.IsDemo, ct);
        if (district == null)
            return await CleanUpStrandedDemoAccountsOnlyAsync(ct);

        var districtId = district.Id;
        var districtName = district.Name;

        // Gather every id reachable from the demo district BEFORE deleting anything.
        var schoolIds = await _context.Schools.Where(s => s.DistrictId == districtId).Select(s => s.Id).ToListAsync(ct);
        var studentIds = await _context.SchoolStudents.Where(s => s.DistrictId == districtId).Select(s => s.Id).ToListAsync(ct);
        var staffUserIds = await _context.StaffProfiles.Where(sp => sp.DistrictId == districtId).Select(sp => sp.UserId).Distinct().ToListAsync(ct);
        var instanceIds = await _context.DocumentInstances.Where(d => studentIds.Contains(d.SchoolStudentId)).Select(d => d.Id).ToListAsync(ct);
        var versionIds = await _context.AuthoredDocumentVersions.Where(v => studentIds.Contains(v.SchoolStudentId)).Select(v => v.Id).ToListAsync(ct);
        var meetingIds = await _context.Meetings.Where(m => studentIds.Contains(m.SchoolStudentId)).Select(m => m.Id).ToListAsync(ct);
        var evaluationCaseIds = await _context.EvaluationCases.Where(e => studentIds.Contains(e.SchoolStudentId)).Select(e => e.Id).ToListAsync(ct);
        var sharedRevisionIds = await _context.SharedDraftRevisions.Where(r => instanceIds.Contains(r.DocumentInstanceId)).Select(r => r.Id).ToListAsync(ct);
        var childLinks = await _context.ChildLinks.Where(cl => studentIds.Contains(cl.SchoolStudentId)).ToListAsync(ct);
        var childProfileIds = childLinks.Where(cl => cl.ChildProfileId.HasValue).Select(cl => cl.ChildProfileId!.Value).Distinct().ToList();
        var studentInvites = await _context.StudentInvites.Where(si => si.SchoolStudentId != null && studentIds.Contains(si.SchoolStudentId.Value)).ToListAsync(ct);
        var consentBlobPaths = await _context.EvaluationCases.Where(e => evaluationCaseIds.Contains(e.Id) && e.ConsentBlobPath != null).Select(e => e.ConsentBlobPath!).ToListAsync(ct);
        var exportJobs = await _context.ExportJobs.Where(j => j.DistrictId == districtId).ToListAsync(ct);

        // Parent users whose ONLY footprint is this demo district: every ChildLink for every ChildProfile
        // they own points at a demo student.
        var candidateParentUserIds = await _context.ChildProfiles.Where(cp => childProfileIds.Contains(cp.Id)).Select(cp => cp.UserId).Distinct().ToListAsync(ct);
        var demoOnlyParentUserIds = new List<int>();
        foreach (var parentUserId in candidateParentUserIds)
        {
            var theirChildProfileIds = await _context.ChildProfiles.Where(cp => cp.UserId == parentUserId).Select(cp => cp.Id).ToListAsync(ct);
            var theirLinkedStudentIds = await _context.ChildLinks
                .Where(cl => cl.ChildProfileId != null && theirChildProfileIds.Contains(cl.ChildProfileId.Value))
                .Select(cl => cl.SchoolStudentId)
                .ToListAsync(ct);
            if (theirLinkedStudentIds.Count > 0 && theirLinkedStudentIds.All(sid => studentIds.Contains(sid)))
                demoOnlyParentUserIds.Add(parentUserId);
        }

        // Student-account users: their StudentProfile's SchoolStudentId is one of the demo students.
        var studentAccountUserIds = await _context.StudentProfiles
            .Where(sp => sp.SchoolStudentId != null && studentIds.Contains(sp.SchoolStudentId.Value))
            .Select(sp => sp.UserId)
            .Distinct()
            .ToListAsync(ct);

        // Belt-and-suspenders: a parent/student account CreateAccountUserAsync created but whose
        // subsequent invite/link/consent step never completed (e.g. an interrupted prior seed run) has
        // no ChildProfile or StudentProfile yet, so neither structured check above catches it. Every
        // demo account uses this fixed, reserved fictional domain — never a real user's — so sweeping on
        // it is safe and guarantees a reset leaves no orphaned account behind.
        var strandedDemoEmailUserIds = await _context.Users
            .Where(u => u.Email.ToLower().EndsWith("@" + EmailDomain))
            .Select(u => u.Id)
            .ToListAsync(ct);

        // Every account this demo district owns: its staff, the parents whose only footprint is this
        // district, its student account(s), and anything left on the reserved fictional domain.
        var demoAccountUserIds = staffUserIds
            .Concat(demoOnlyParentUserIds)
            .Concat(studentAccountUserIds)
            .Concat(strandedDemoEmailUserIds)
            .Distinct()
            .ToList();

        var isSqlServer = _context.Database.IsSqlServer();

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            if (isSqlServer)
                foreach (var (trigger, table) in ImmutabilityTriggers)
                    // Trigger/table names are from the fixed, hardcoded array above — never user input —
                    // so string concatenation (not interpolation) is used deliberately to sidestep the
                    // EF1002 analyzer, which cannot distinguish a safe fixed identifier from unsafe input
                    // and flags any interpolated-string argument to ExecuteSqlRaw. Identifiers like
                    // trigger/table names cannot be parameterized as SQL parameters in any case.
                    await _context.Database.ExecuteSqlRawAsync("DISABLE TRIGGER " + trigger + " ON " + table + ";", ct);

            // Bypasses ImmutableVersionInterceptor for the AuthoredDocumentVersion deletes below, for the
            // lifetime of this scope only (see ImmutabilityGuardBypass doc).
            _immutabilityBypass.Enabled = true;

            // ---- Goals ----
            var goalRecordIds = await _context.GoalRecords.Where(g => versionIds.Contains(g.AuthoredDocumentVersionId)).Select(g => g.Id).ToListAsync(ct);
            _context.GoalObservations.RemoveRange(await _context.GoalObservations.Where(o => goalRecordIds.Contains(o.GoalRecordId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.GoalRecords.RemoveRange(await _context.GoalRecords.Where(g => goalRecordIds.Contains(g.Id)).ToListAsync(ct));
            _context.GoalRetirements.RemoveRange(await _context.GoalRetirements.Where(r => instanceIds.Contains(r.DocumentInstanceId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Shared drafts (family review) ----
            _context.DraftAcknowledgements.RemoveRange(await _context.DraftAcknowledgements.Where(a => sharedRevisionIds.Contains(a.SharedDraftRevisionId)).ToListAsync(ct));
            _context.DraftResponses.RemoveRange(await _context.DraftResponses.Where(r => sharedRevisionIds.Contains(r.SharedDraftRevisionId)).ToListAsync(ct));
            _context.ParentDraftNotes.RemoveRange(await _context.ParentDraftNotes.Where(n => sharedRevisionIds.Contains(n.SharedDraftRevisionId)).ToListAsync(ct));
            _context.SharedDraftExplanations.RemoveRange(await _context.SharedDraftExplanations.Where(e => sharedRevisionIds.Contains(e.SharedDraftRevisionId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.SharedDraftRevisions.RemoveRange(await _context.SharedDraftRevisions.Where(r => sharedRevisionIds.Contains(r.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Meetings ----
            _context.MeetingDecisions.RemoveRange(await _context.MeetingDecisions.Where(d => meetingIds.Contains(d.MeetingId)).ToListAsync(ct));
            _context.MeetingSummaries.RemoveRange(await _context.MeetingSummaries.Where(s => meetingIds.Contains(s.MeetingId)).ToListAsync(ct));
            _context.MeetingReminders.RemoveRange(await _context.MeetingReminders.Where(r => meetingIds.Contains(r.MeetingId)).ToListAsync(ct));
            _context.MeetingParticipants.RemoveRange(await _context.MeetingParticipants.Where(p => meetingIds.Contains(p.MeetingId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.Meetings.RemoveRange(await _context.Meetings.Where(m => meetingIds.Contains(m.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Evaluation cases ----
            _context.EvaluatorAssignments.RemoveRange(await _context.EvaluatorAssignments.Where(a => evaluationCaseIds.Contains(a.EvaluationCaseId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            foreach (var path in consentBlobPaths)
                await TryDeleteBlobAsync(path, ct);
            _context.EvaluationCases.RemoveRange(await _context.EvaluationCases.Where(e => evaluationCaseIds.Contains(e.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Family contact / offline input ----
            _context.FamilyContactAttempts.RemoveRange(await _context.FamilyContactAttempts.Where(a => studentIds.Contains(a.SchoolStudentId)).ToListAsync(ct));
            _context.OfflineFamilyInputs.RemoveRange(await _context.OfflineFamilyInputs.Where(o => studentIds.Contains(o.SchoolStudentId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Signed artifacts / signature events (defensive — the seeder itself creates none) ----
            var signedArtifacts = await _context.SignedArtifacts.Where(sa => versionIds.Contains(sa.AuthoredDocumentVersionId)).ToListAsync(ct);
            foreach (var artifact in signedArtifacts)
                await TryDeleteBlobAsync(artifact.BlobPath, ct);
            _context.SignedArtifacts.RemoveRange(signedArtifacts);
            _context.SignatureEvents.RemoveRange(await _context.SignatureEvents.Where(se => versionIds.Contains(se.AuthoredDocumentVersionId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Documents (PDF blob + immutable AuthoredDocumentVersion, then the Draft instance) ----
            var pdfs = await _context.AuthoredDocumentPdfs.Where(p => versionIds.Contains(p.AuthoredDocumentVersionId)).ToListAsync(ct);
            var versionNumberById = await _context.AuthoredDocumentVersions.Where(v => versionIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.VersionNumber, ct);
            foreach (var pdf in pdfs.Where(p => p.RenderStatus == PdfRenderStatus.Rendered))
                if (versionNumberById.TryGetValue(pdf.AuthoredDocumentVersionId, out var versionNumber))
                    await TryDeleteBlobAsync(IAuthoredDocumentPdfService.BlobPathFor(pdf.AuthoredDocumentVersionId, versionNumber), ct);
            _context.AuthoredDocumentPdfs.RemoveRange(pdfs);
            await _context.SaveChangesAsync(ct);

            // Guarded by both the C# interceptor (bypassed above) and the SQL Server trigger (disabled above).
            _context.AuthoredDocumentVersions.RemoveRange(await _context.AuthoredDocumentVersions.Where(v => versionIds.Contains(v.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            _context.DocumentInstances.RemoveRange(await _context.DocumentInstances.Where(d => instanceIds.Contains(d.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Team + access ----
            _context.StudentTeamMembers.RemoveRange(await _context.StudentTeamMembers.Where(m => studentIds.Contains(m.SchoolStudentId)).ToListAsync(ct));
            _context.SchoolStudentAccesses.RemoveRange(await _context.SchoolStudentAccesses.Where(a => studentIds.Contains(a.SchoolStudentId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Family links + student invites (before ChildProfile/StudentProfile/SchoolStudent) ----
            _context.ChildLinks.RemoveRange(childLinks);
            _context.StudentInvites.RemoveRange(studentInvites);
            await _context.SaveChangesAsync(ct);

            // Also sweep any StudentProfile/ChildProfile owned by a stranded demo-email user that never
            // got as far as linking to a demo student (e.g. an interrupted prior seed run) — these
            // wouldn't otherwise be reachable from studentIds/childProfileIds.
            _context.StudentProfiles.RemoveRange(await _context.StudentProfiles
                .Where(sp => (sp.SchoolStudentId != null && studentIds.Contains(sp.SchoolStudentId.Value)) || strandedDemoEmailUserIds.Contains(sp.UserId))
                .ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            var strandedChildProfileIds = await _context.ChildProfiles
                .Where(cp => strandedDemoEmailUserIds.Contains(cp.UserId))
                .Select(cp => cp.Id)
                .ToListAsync(ct);
            var allChildProfileIds = childProfileIds.Concat(strandedChildProfileIds).Distinct().ToList();

            // ---- Parent-product rows that block the ChildProfile/User/District deletes ----
            // These five FKs are NO ACTION, so a row in any of them stops the delete below; everything else
            // hanging off a ChildProfile (journal entries, prep questions, advocacy goals, analysis runs)
            // cascades and needs no help. Usage records matter most in practice: they appear the moment
            // anyone actually demos an AI feature, which is exactly when the next reset has to work.
            _context.UsageRecords.RemoveRange(await _context.UsageRecords
                .Where(r => (r.ChildProfileId != null && allChildProfileIds.Contains(r.ChildProfileId.Value))
                            || demoAccountUserIds.Contains(r.UserId)
                            || r.DistrictId == districtId)
                .ToListAsync(ct));
            _context.AdvocateThreads.RemoveRange(await _context.AdvocateThreads
                .Where(t => allChildProfileIds.Contains(t.ChildProfileId) || demoAccountUserIds.Contains(t.ParentUserId))
                .ToListAsync(ct));
            _context.MeetingPrepChecklists.RemoveRange(await _context.MeetingPrepChecklists
                .Where(c => allChildProfileIds.Contains(c.ChildProfileId)).ToListAsync(ct));
            _context.ProgressReports.RemoveRange(await _context.ProgressReports
                .Where(r => allChildProfileIds.Contains(r.ChildProfileId)).ToListAsync(ct));
            _context.StudentWorkspaces.RemoveRange(await _context.StudentWorkspaces
                .Where(w => demoAccountUserIds.Contains(w.UserId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            _context.ChildAccesses.RemoveRange(await _context.ChildAccesses.Where(a => allChildProfileIds.Contains(a.ChildProfileId)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.ChildProfiles.RemoveRange(await _context.ChildProfiles.Where(cp => allChildProfileIds.Contains(cp.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            // ---- Export jobs ----
            foreach (var job in exportJobs.Where(j => !string.IsNullOrEmpty(j.BlobPath)))
                await TryDeleteBlobAsync(job.BlobPath!, ct);
            _context.ExportJobs.RemoveRange(exportJobs);
            await _context.SaveChangesAsync(ct);

            // ---- Roster + org ----
            _context.SchoolStudents.RemoveRange(await _context.SchoolStudents.Where(s => studentIds.Contains(s.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.StaffInvites.RemoveRange(await _context.StaffInvites.Where(i => i.DistrictId == districtId).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.StaffProfiles.RemoveRange(await _context.StaffProfiles.Where(sp => sp.DistrictId == districtId).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);
            _context.Schools.RemoveRange(await _context.Schools.Where(s => schoolIds.Contains(s.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            var trackedDistrict = await _context.Districts.FirstAsync(d => d.Id == districtId, ct);
            _context.Districts.Remove(trackedDistrict);
            await _context.SaveChangesAsync(ct);

            // ---- Users whose only footprint was this demo district ----
            var userIdsToDelete = demoAccountUserIds;

            // BetaInviteCode.RedeemedByUserId is FK Restrict — CreateAccountUserAsync inserts one of
            // these per parent/student account (the closed-beta gate RegisterAsync requires); they must
            // go before the User rows they were redeemed by.
            _context.Set<BetaInviteCode>().RemoveRange(
                await _context.Set<BetaInviteCode>().Where(b => b.RedeemedByUserId != null && userIdsToDelete.Contains(b.RedeemedByUserId.Value)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            _context.Users.RemoveRange(await _context.Users.Where(u => userIdsToDelete.Contains(u.Id)).ToListAsync(ct));
            await _context.SaveChangesAsync(ct);

            _immutabilityBypass.Enabled = false;
            if (isSqlServer)
                foreach (var (trigger, table) in ImmutabilityTriggers)
                    await _context.Database.ExecuteSqlRawAsync("ENABLE TRIGGER " + trigger + " ON " + table + ";", ct);

            await transaction.CommitAsync(ct);

            return DemoSeedResult.Ok(
                $"Reset demo district '{districtName}' (id {districtId}): removed {studentIds.Count} students, " +
                $"{staffUserIds.Count} staff, {demoOnlyParentUserIds.Count} parent(s), {studentAccountUserIds.Count} student account(s).",
                Array.Empty<DemoLoginRow>());
        }
        catch
        {
            _immutabilityBypass.Enabled = false;

            // A transport-level failure against Azure SQL kills the connection, and with it the
            // transaction — the server has already rolled it back. Rolling back again throws "This
            // SqlTransaction has completed", which would replace the exception that actually explains what
            // happened. The rollback is still attempted, because an ordinary failure (a constraint, say)
            // leaves a live transaction that must be undone.
            try
            {
                await transaction.RollbackAsync(ct);
            }
            catch (Exception rollbackFailure)
            {
                _logger.LogWarning(rollbackFailure, "Demo reset: the transaction could not be rolled back explicitly — the connection had already ended it.");
            }

            throw;
        }
    }

    /// <summary>
    /// When no demo district exists, still sweep any lingering "@mapleridge.example" account left behind by
    /// an interrupted prior <c>SeedAsync</c> run (e.g. it crashed partway through
    /// <see cref="CreateFamilyEngagementAsync"/> after minting the account but before linking it to a
    /// student — nothing scoped to a district catches that). A true no-op only when none exist either.
    /// </summary>
    private async Task<DemoSeedResult> CleanUpStrandedDemoAccountsOnlyAsync(CancellationToken ct)
    {
        var strandedUserIds = await _context.Users
            .Where(u => u.Email.ToLower().EndsWith("@" + EmailDomain))
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (strandedUserIds.Count == 0)
            return DemoSeedResult.NoOpResult("No demo district found — nothing to reset.");

        _context.Set<BetaInviteCode>().RemoveRange(
            await _context.Set<BetaInviteCode>().Where(b => b.RedeemedByUserId != null && strandedUserIds.Contains(b.RedeemedByUserId.Value)).ToListAsync(ct));
        await _context.SaveChangesAsync(ct);

        var childProfileIds = await _context.ChildProfiles.Where(cp => strandedUserIds.Contains(cp.UserId)).Select(cp => cp.Id).ToListAsync(ct);

        // Same NO ACTION references ResetAsync clears — see the comment there.
        _context.UsageRecords.RemoveRange(await _context.UsageRecords
            .Where(r => (r.ChildProfileId != null && childProfileIds.Contains(r.ChildProfileId.Value)) || strandedUserIds.Contains(r.UserId))
            .ToListAsync(ct));
        _context.AdvocateThreads.RemoveRange(await _context.AdvocateThreads
            .Where(t => childProfileIds.Contains(t.ChildProfileId) || strandedUserIds.Contains(t.ParentUserId))
            .ToListAsync(ct));
        _context.MeetingPrepChecklists.RemoveRange(await _context.MeetingPrepChecklists
            .Where(c => childProfileIds.Contains(c.ChildProfileId)).ToListAsync(ct));
        _context.ProgressReports.RemoveRange(await _context.ProgressReports
            .Where(r => childProfileIds.Contains(r.ChildProfileId)).ToListAsync(ct));
        _context.StudentWorkspaces.RemoveRange(await _context.StudentWorkspaces
            .Where(w => strandedUserIds.Contains(w.UserId)).ToListAsync(ct));
        await _context.SaveChangesAsync(ct);

        _context.ChildAccesses.RemoveRange(await _context.ChildAccesses.Where(a => childProfileIds.Contains(a.ChildProfileId)).ToListAsync(ct));
        await _context.SaveChangesAsync(ct);
        _context.ChildProfiles.RemoveRange(await _context.ChildProfiles.Where(cp => childProfileIds.Contains(cp.Id)).ToListAsync(ct));
        _context.StudentProfiles.RemoveRange(await _context.StudentProfiles.Where(sp => strandedUserIds.Contains(sp.UserId)).ToListAsync(ct));
        await _context.SaveChangesAsync(ct);

        _context.Users.RemoveRange(await _context.Users.Where(u => strandedUserIds.Contains(u.Id)).ToListAsync(ct));
        await _context.SaveChangesAsync(ct);

        return DemoSeedResult.Ok(
            $"No demo district found, but cleaned up {strandedUserIds.Count} stranded demo account(s) left behind by an interrupted prior run.",
            Array.Empty<DemoLoginRow>());
    }

    private async Task TryDeleteBlobAsync(string blobPath, CancellationToken ct)
    {
        try
        {
            await _blobStorage.DeleteAsync(blobPath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Demo reset: failed to delete blob '{BlobPath}' — continuing.", blobPath);
        }
    }

    // ================================================================================= Token extraction

    private static readonly Regex TokenRegex = new(@"token=([^""&\s]+)", RegexOptions.Compiled);

    /// <summary>
    /// Every invite email is composed by <c>IEmailService</c> and enqueued (not sent) as an
    /// <see cref="OutboundEmail"/> row — this reads the raw token back out of the newest matching row's
    /// body instead of re-deriving it, so the seeder drives the exact same invite/accept pipeline a real
    /// admin does (no shortcut hashing/token minting of its own).
    /// </summary>
    private async Task<string> ExtractLatestTokenAsync(string toEmail, string kind, CancellationToken ct)
    {
        var body = await _context.OutboundEmails
            .Where(e => e.ToEmail == toEmail && e.Kind == kind)
            .OrderByDescending(e => e.Id)
            .Select(e => e.HtmlBody + " " + e.TextBody)
            .FirstOrDefaultAsync(ct);

        if (body == null)
            throw new InvalidOperationException($"No queued '{kind}' email found for {toEmail} — cannot extract invite token.");

        var match = TokenRegex.Match(body);
        if (!match.Success)
            throw new InvalidOperationException($"Could not find a token in the queued '{kind}' email for {toEmail}.");

        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}
