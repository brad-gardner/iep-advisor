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
    private const string DistrictName = "Maple Ridge Local Schools";
    private const string EmailDomain = "mapleridge.example";

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
                $"Demo district '{existing.Name}' (id {existing.Id}) already exists — nothing to do. Run `seed-demo --reset` first to rebuild it.");

        var logins = new List<DemoLoginRow>();

        _logger.LogInformation("Demo seed: creating district + admin…");
        var (districtId, districtAdminUserId) = await CreateDistrictAndAdminAsync(logins, ct);
        _logger.LogInformation("Demo seed: creating schools…");
        var schoolIds = await CreateSchoolsAsync(districtAdminUserId, ct);
        _logger.LogInformation("Demo seed: creating staff…");
        var staff = await CreateStaffAsync(districtAdminUserId, schoolIds, logins, ct);
        _logger.LogInformation("Demo seed: creating students + teams…");
        var students = await CreateStudentsAsync(districtAdminUserId, schoolIds, staff, ct);
        _logger.LogInformation("Demo seed: creating documents (IEPs/ETRs) + goals…");
        await CreateDocumentsAsync(districtAdminUserId, students, ct);
        _logger.LogInformation("Demo seed: creating meetings…");
        await CreateMeetingsAsync(districtAdminUserId, students, ct);
        _logger.LogInformation("Demo seed: creating family engagement (parents + student account)…");
        await CreateFamilyEngagementAsync(districtAdminUserId, staff, students, logins, ct);
        _logger.LogInformation("Demo seed: creating evaluation case…");
        await CreateEvaluationCaseAsync(districtAdminUserId, students, ct);
        _logger.LogInformation("Demo seed: creating contact attempts…");
        await CreateContactAttemptsAsync(districtAdminUserId, students, ct);

        var message = $"Seeded demo district '{DistrictName}' (id {districtId}): {schoolIds.Count} schools, " +
                      $"{staff.Count + 1} staff, {students.Count} students."; // +1: staff excludes the district admin
        return DemoSeedResult.Ok(message, logins);
    }

    // ================================================================================= District + admin

    private async Task<(int DistrictId, int AdminUserId)> CreateDistrictAndAdminAsync(List<DemoLoginRow> logins, CancellationToken ct)
    {
        const string email = $"dana.superintendent@{EmailDomain}";
        var register = await _authService.RegisterDistrictAsync(new RegisterDistrictModel
        {
            Email = email,
            Password = DemoPassword,
            FirstName = "Dana",
            LastName = "Superintendent",
            DistrictName = DistrictName,
            StateCode = "OH"
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

        logins.Add(new DemoLoginRow("District Admin", email, DemoPassword));
        return (districtId, adminUserId);
    }

    // ================================================================================= Schools

    private async Task<List<int>> CreateSchoolsAsync(int districtAdminUserId, CancellationToken ct)
    {
        var names = new[] { "Maple Ridge Elementary", "Maple Ridge Middle School", "Maple Ridge High School" };
        var ids = new List<int>();
        foreach (var name in names)
        {
            var result = await _districtService.CreateSchoolAsync(districtAdminUserId, new CreateSchoolModel { Name = name, StateCode = "OH" }, ct);
            if (!result.Success || result.Data == null)
                throw new InvalidOperationException($"Failed to create school '{name}': {result.Message}");
            ids.Add(result.Data.Id);
        }
        return ids;
    }

    // ================================================================================= Staff

    public sealed record DemoStaff(int UserId, int StaffProfileId, int OrgRoleId, int? SchoolId, string Email, string FirstName, string LastName);

    private async Task<List<DemoStaff>> CreateStaffAsync(int districtAdminUserId, List<int> schoolIds, List<DemoLoginRow> logins, CancellationToken ct)
    {
        var staff = new List<DemoStaff>();

        var schoolAdmin = await InviteAndAcceptStaffAsync(districtAdminUserId, $"pat.principal@{EmailDomain}", "Pat", "Principal", OrgRoleIds.SchoolAdmin, schoolIds[0], ct);
        staff.Add(schoolAdmin);
        logins.Add(new DemoLoginRow("School Admin", schoolAdmin.Email, DemoPassword));

        var teacherNames = new[] { ("jordan.teacher", "Jordan", "Rivera"), ("morgan.teacher", "Morgan", "Chen"), ("casey.teacher", "Casey", "Nguyen") };
        for (var i = 0; i < teacherNames.Length; i++)
        {
            var (local, first, last) = teacherNames[i];
            var teacher = await InviteAndAcceptStaffAsync(districtAdminUserId, $"{local}@{EmailDomain}", first, last, OrgRoleIds.Teacher, schoolIds[i % schoolIds.Count], ct);
            staff.Add(teacher);
            logins.Add(new DemoLoginRow("Teacher / Case Manager", teacher.Email, DemoPassword));
        }

        var providerNames = new[] { ("sam.slp", "Sam", "Okafor"), ("robin.ot", "Robin", "Alvarez") };
        for (var i = 0; i < providerNames.Length; i++)
        {
            var (local, first, last) = providerNames[i];
            var provider = await InviteAndAcceptStaffAsync(districtAdminUserId, $"{local}@{EmailDomain}", first, last, OrgRoleIds.RelatedServiceProvider, schoolIds[i % schoolIds.Count], ct);
            staff.Add(provider);
            logins.Add(new DemoLoginRow("Related Service Provider", provider.Email, DemoPassword));
        }

        var genEdNames = new[] { ("avery.gened", "Avery", "Thompson"), ("riley.gened", "Riley", "Patel") };
        for (var i = 0; i < genEdNames.Length; i++)
        {
            var (local, first, last) = genEdNames[i];
            var genEd = await InviteAndAcceptStaffAsync(districtAdminUserId, $"{local}@{EmailDomain}", first, last, OrgRoleIds.GeneralEducator, schoolIds[i % schoolIds.Count], ct);
            staff.Add(genEd);
            logins.Add(new DemoLoginRow("General Educator", genEd.Email, DemoPassword));
        }

        return staff;
    }

    private async Task<DemoStaff> InviteAndAcceptStaffAsync(int callerUserId, string email, string firstName, string lastName, int orgRoleId, int? schoolId, CancellationToken ct)
    {
        var invite = await _staffInviteService.InviteAsync(callerUserId, new CreateStaffInviteModel { Email = email, OrgRoleId = orgRoleId, SchoolId = schoolId }, ct);
        if (!invite.Success)
            throw new InvalidOperationException($"Failed to invite staff '{email}': {invite.Message}");

        var rawToken = await ExtractLatestTokenAsync(email, "StaffInvite", ct);
        var accept = await _staffInviteService.AcceptAsync(new AcceptStaffInviteModel { Token = rawToken, FirstName = firstName, LastName = lastName, Password = DemoPassword }, ct);
        if (!accept.Success || accept.AuthResult == null)
            throw new InvalidOperationException($"Failed to accept staff invite for '{email}': {accept.Message}");

        var userId = accept.AuthResult.User.Id;
        var staffProfileId = await _context.StaffProfiles.Where(sp => sp.UserId == userId).Select(sp => sp.Id).FirstAsync(ct);
        return new DemoStaff(userId, staffProfileId, orgRoleId, schoolId, email, firstName, lastName);
    }

    // ================================================================================= Students

    public sealed record DemoStudent(int Id, int SchoolId, string FirstName, string LastName, int CaseManagerStaffProfileId, bool HasIep, bool HasEtr);

    private static readonly string[] FirstNames =
    {
        "Aiden", "Bella", "Carlos", "Daniela", "Ethan", "Fiona", "Gavin", "Hannah", "Isaac", "Julia",
        "Kaleb", "Layla", "Mason", "Nadia", "Owen", "Priya", "Quinn", "Ruby", "Samuel", "Talia",
        "Uriel", "Violet", "Wesley", "Ximena", "Yusuf", "Zoe", "Adrian", "Brooke", "Caleb", "Destiny",
        "Elias", "Faith", "Gabriel", "Harper", "Ian", "Jasmine", "Kevin", "Lucia", "Marcus", "Nora"
    };

    private static readonly string[] LastNames =
    {
        "Bennett", "Carter", "Diaz", "Edwards", "Flores", "Grant", "Hayes", "Ibarra", "Jenkins", "Kim",
        "Lopez", "Mitchell", "Nguyen", "Ortiz", "Parker", "Quinones", "Reyes", "Sanders", "Torres", "Underwood"
    };

    private static readonly GradeLevel[] ElementaryGrades = { GradeLevel.K, GradeLevel.G1, GradeLevel.G2, GradeLevel.G3, GradeLevel.G4, GradeLevel.G5 };
    private static readonly GradeLevel[] MiddleGrades = { GradeLevel.G6, GradeLevel.G7, GradeLevel.G8 };
    private static readonly GradeLevel[] HighGrades = { GradeLevel.G9, GradeLevel.G10, GradeLevel.G11, GradeLevel.G12 };
    private static readonly DisabilityCategory[] Disabilities = Enum.GetValues<DisabilityCategory>();

    private async Task<List<DemoStudent>> CreateStudentsAsync(int districtAdminUserId, List<int> schoolIds, List<DemoStaff> staff, CancellationToken ct)
    {
        var teachers = staff.Where(s => s.OrgRoleId == OrgRoleIds.Teacher).ToList();
        var providers = staff.Where(s => s.OrgRoleId == OrgRoleIds.RelatedServiceProvider).ToList();
        var genEds = staff.Where(s => s.OrgRoleId == OrgRoleIds.GeneralEducator).ToList();

        var students = new List<DemoStudent>();
        var today = DateTime.UtcNow.Date;

        const int total = 40;
        for (var i = 0; i < total; i++)
        {
            var schoolIndex = i < 14 ? 0 : i < 27 ? 1 : 2;
            var schoolId = schoolIds[schoolIndex];
            var gradeBand = schoolIndex == 0 ? ElementaryGrades : schoolIndex == 1 ? MiddleGrades : HighGrades;
            var grade = gradeBand[i % gradeBand.Length];
            var disability = Disabilities[i % Disabilities.Length];
            var firstName = FirstNames[i % FirstNames.Length];
            var lastName = LastNames[(i * 7) % LastNames.Length];

            // Spread review/reevaluation dates over roughly ±90 days from today, including some already
            // overdue (negative offsets), per the plan-8 contract.
            var offsetDays = (i * 180 / (total - 1)) - 90;

            var createResult = await _educatorService.CreateStudentAsync(districtAdminUserId, new CreateSchoolStudentModel
            {
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = today.AddYears(-(5 + GradeIndex(grade))).AddDays(-i),
                ExternalStudentId = $"MR-{1000 + i}",
                GradeLevel = grade,
                DisabilityCategory = disability,
                HomeLanguage = "en",
                IepDate = today.AddDays(offsetDays - 365),
                AnnualReviewDueDate = today.AddDays(offsetDays),
                EtrDate = today.AddDays(offsetDays - 700),
                ReevaluationDueDate = today.AddDays(offsetDays + 20),
                SchoolId = schoolId
            }, ct);

            if (!createResult.Success || createResult.Data == null)
                throw new InvalidOperationException($"Failed to create student '{firstName} {lastName}': {createResult.Message}");

            var studentId = createResult.Data.Id;

            // Case manager: the teacher based at THIS student's school (StudentTeamWriter.ValidateTeamCandidate
            // requires a Teacher/GeneralEducator to match the student's SchoolId — only RelatedServiceProvider
            // is exempt — so this must follow school assignment, not a global round-robin across all 3 teachers).
            var caseManager = teachers[schoolIndex];
            var addCaseManager = await _studentTeamService.AddMemberAsync(districtAdminUserId, studentId, new AddTeamMemberModel
            {
                StaffProfileId = caseManager.StaffProfileId,
                TeamRole = TeamRole.CaseManager,
                IsLead = true
            }, ct);
            if (!addCaseManager.Success)
                throw new InvalidOperationException($"Failed to assign case manager for student {studentId}: {addCaseManager.Message}");

            // A slice of students also get a related-service provider or general educator on their team,
            // for realism (not required by the plan, but cheap and makes team rosters look genuine).
            // RelatedServiceProvider is exempt from the same-school rule (may serve any school in the
            // district), so round-robin is fine there; GeneralEducator is NOT exempt, so it must match
            // this student's school like the case manager above (skipped if no genEd is based here).
            if (i < 10)
            {
                var provider = providers[i % providers.Count];
                await _studentTeamService.AddMemberAsync(districtAdminUserId, studentId, new AddTeamMemberModel
                {
                    StaffProfileId = provider.StaffProfileId,
                    TeamRole = provider.FirstName == "Sam" ? TeamRole.SpeechLanguagePathologist : TeamRole.OccupationalTherapist
                }, ct);
            }
            else if (i < 20)
            {
                var genEd = genEds.FirstOrDefault(g => g.SchoolId == schoolId);
                if (genEd != null)
                    await _studentTeamService.AddMemberAsync(districtAdminUserId, studentId, new AddTeamMemberModel
                    {
                        StaffProfileId = genEd.StaffProfileId,
                        TeamRole = TeamRole.GeneralEducationTeacher
                    }, ct);
            }

            students.Add(new DemoStudent(studentId, schoolId, firstName, lastName, caseManager.StaffProfileId, HasIep: i < 15, HasEtr: i is >= 15 and < 20));

            if ((i + 1) % 10 == 0)
                _logger.LogInformation("Demo seed: created {Count}/{Total} students…", i + 1, total);
        }

        return students;
    }

    private static int GradeIndex(GradeLevel grade) => grade switch
    {
        GradeLevel.PK => -1,
        GradeLevel.K => 0,
        GradeLevel.Ungraded => 8,
        _ => int.Parse(grade.ToString().TrimStart('G'))
    };

    // ================================================================================= Documents (IEPs/ETRs) + goals

    private async Task CreateDocumentsAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var iepTypeId = await _context.DocumentTypes.Where(t => t.Key == "IEP").Select(t => t.Id).FirstAsync(ct);
        var etrTypeId = await _context.DocumentTypes.Where(t => t.Key == "ETR").Select(t => t.Id).FirstAsync(ct);

        var iepCount = 0;
        foreach (var student in students.Where(s => s.HasIep))
        {
            await CreateFinalizedIepAsync(actingUserId, student, iepTypeId, ct);
            iepCount++;
            _logger.LogInformation("Demo seed: finalized IEP {Count} for student {StudentId}.", iepCount, student.Id);
        }

        var etrCount = 0;
        foreach (var student in students.Where(s => s.HasEtr))
        {
            await CreateFinalizedEtrAsync(actingUserId, student, etrTypeId, ct);
            etrCount++;
            _logger.LogInformation("Demo seed: finalized ETR {Count} for student {StudentId}.", etrCount, student.Id);
        }
    }

    private async Task CreateFinalizedIepAsync(int actingUserId, DemoStudent student, int iepTypeId, CancellationToken ct)
    {
        var create = await _documentInstanceService.CreateAsync(student.Id, iepTypeId, actingUserId, ct);
        if (!create.Success || create.Data == null)
            throw new InvalidOperationException($"Failed to create IEP draft for student {student.Id}: {create.Message}");

        var semantics = await LoadSemanticsAsync(create.Data.DocumentTemplateVersionId, ct);
        var patch = new Dictionary<string, JsonElement>();

        SetScalar(patch, semantics, FieldSemantics.StudentProfile,
            $"{student.FirstName} {student.LastName} is a student at Maple Ridge who benefits from consistent, individualized support across the school day.");
        SetScalar(patch, semantics, FieldSemantics.PresentLevels,
            $"{student.FirstName} currently participates in the general curriculum with support. Recent classroom-based assessments show steady, gradual progress toward grade-level expectations.");

        SetTable(patch, semantics, FieldSemantics.Goals, new[]
        {
            new Dictionary<string, string>
            {
                [ColumnSemantics.Domain] = "Reading",
                [ColumnSemantics.GoalText] = $"Given a grade-level passage, {student.FirstName} will read aloud with 95% accuracy on 4 of 5 trials.",
                [ColumnSemantics.Baseline] = $"{student.FirstName} currently reads with 78% accuracy on grade-level passages.",
                [ColumnSemantics.TargetCriteria] = "95% accuracy on 4 of 5 consecutive probes.",
                [ColumnSemantics.MeasurementMethod] = "Weekly curriculum-based reading probes.",
                [ColumnSemantics.Timeframe] = "By the next annual review."
            },
            new Dictionary<string, string>
            {
                [ColumnSemantics.Domain] = "Math",
                [ColumnSemantics.GoalText] = $"{student.FirstName} will solve grade-level multi-step word problems with 80% accuracy on 3 of 4 trials.",
                [ColumnSemantics.Baseline] = $"{student.FirstName} currently solves multi-step word problems with 55% accuracy.",
                [ColumnSemantics.TargetCriteria] = "80% accuracy across 3 consecutive probes.",
                [ColumnSemantics.MeasurementMethod] = "Bi-weekly math probes scored against a rubric.",
                [ColumnSemantics.Timeframe] = "By the next annual review."
            }
        });

        SetTable(patch, semantics, FieldSemantics.Services, new[]
        {
            new Dictionary<string, string>
            {
                [ColumnSemantics.ServiceType] = "Specialized Reading Instruction",
                [ColumnSemantics.Frequency] = "4x per week",
                [ColumnSemantics.Duration] = "30 minutes",
                [ColumnSemantics.Location] = "Resource room",
                [ColumnSemantics.ProviderRole] = "Intervention Specialist"
            }
        });

        SetTable(patch, semantics, FieldSemantics.Accommodations, new[]
        {
            new Dictionary<string, string> { [ColumnSemantics.Category] = "Presentation", [ColumnSemantics.Accommodation] = "Extended time (1.5x) on tests and quizzes" },
            new Dictionary<string, string> { [ColumnSemantics.Category] = "Setting", [ColumnSemantics.Accommodation] = "Preferential seating near instruction" }
        });

        if (patch.Count > 0)
        {
            var save = await _documentInstanceService.SaveValuesAsync(create.Data.Id, patch, create.Data.RowVersion, actingUserId, ct);
            if (!save.Success)
                throw new InvalidOperationException($"Failed to save IEP values for student {student.Id}: {save.Message}");
        }

        var finalize = await _authoredDocumentVersionService.FinalizeAsync(create.Data.Id, actingUserId, ct);
        if (!finalize.Success || finalize.Data == null)
            throw new InvalidOperationException($"Failed to finalize IEP for student {student.Id}: {finalize.Message}");

        await _pdfQueue.EnqueueAsync(finalize.Data.Id, CancellationToken.None);

        await AddGoalObservationsAsync(actingUserId, student.Id, ct);
    }

    private async Task CreateFinalizedEtrAsync(int actingUserId, DemoStudent student, int etrTypeId, CancellationToken ct)
    {
        var create = await _documentInstanceService.CreateAsync(student.Id, etrTypeId, actingUserId, ct);
        if (!create.Success || create.Data == null)
            throw new InvalidOperationException($"Failed to create ETR draft for student {student.Id}: {create.Message}");

        var semantics = await LoadSemanticsAsync(create.Data.DocumentTemplateVersionId, ct);
        var patch = new Dictionary<string, JsonElement>();

        SetScalar(patch, semantics, FieldSemantics.ReferralReason,
            $"{student.FirstName} {student.LastName} was referred for evaluation due to continued difficulty accessing grade-level instruction despite classroom interventions.");
        SetScalar(patch, semantics, FieldSemantics.TeamSummary,
            $"The evaluation team reviewed classroom data, standardized assessment results, and family input for {student.FirstName}. Findings support continued specially designed instruction.");

        if (patch.Count > 0)
        {
            var save = await _documentInstanceService.SaveValuesAsync(create.Data.Id, patch, create.Data.RowVersion, actingUserId, ct);
            if (!save.Success)
                throw new InvalidOperationException($"Failed to save ETR values for student {student.Id}: {save.Message}");
        }

        var finalize = await _authoredDocumentVersionService.FinalizeAsync(create.Data.Id, actingUserId, ct);
        if (!finalize.Success || finalize.Data == null)
            throw new InvalidOperationException($"Failed to finalize ETR for student {student.Id}: {finalize.Message}");

        await _pdfQueue.EnqueueAsync(finalize.Data.Id, CancellationToken.None);
    }

    private async Task AddGoalObservationsAsync(int actingUserId, int studentId, CancellationToken ct)
    {
        var goals = await _goalRecordService.GetForStudentAsync(actingUserId, studentId, ct);
        if (!goals.Success || goals.Data == null)
            return;

        var random = new Random(studentId); // deterministic per student
        foreach (var goal in goals.Data)
        {
            var observationCount = 2 + random.Next(3); // 2-4 observations
            for (var i = 0; i < observationCount; i++)
            {
                await _goalRecordService.AddObservationAsync(actingUserId, goal.Id, new CreateGoalObservationModel
                {
                    ObservedAt = DateTime.UtcNow.AddDays(-((observationCount - i) * 14)),
                    Value = 60 + random.Next(35),
                    Unit = goal.Domain == "Math" ? "% accuracy" : "WCPM",
                    Note = "Weekly progress-monitoring probe."
                }, ct);
            }
        }
    }

    /// <summary>Reads the pinned template version's semantic field/column map, the same mechanism
    /// <c>GoalRecordService.ProjectOnFinalizeAsync</c> uses to find "the Goals table" without a hardcoded
    /// FieldKey.</summary>
    private async Task<IReadOnlyDictionary<string, SemanticField>> LoadSemanticsAsync(int templateVersionId, CancellationToken ct)
    {
        var sections = await _context.TemplateSections
            .Where(s => s.DocumentTemplateVersionId == templateVersionId)
            .Include(s => s.Fields)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);
        return TemplateSemanticsReader.Read(sections);
    }

    /// <summary>Sets a Text/RichText field's value from the semantic map, if the template has one tagged
    /// with that semantic. Select/Date/Checkbox are intentionally left untouched (no confirmed literal
    /// option strings to fabricate safely) — the seeded templates have no required fields, so leaving
    /// them blank never blocks finalize.</summary>
    private static void SetScalar(Dictionary<string, JsonElement> patch, IReadOnlyDictionary<string, SemanticField> semantics, string semanticKey, string value)
    {
        if (!semantics.TryGetValue(semanticKey, out var field))
            return;
        if (field.FieldType is FieldType.Text or FieldType.RichText)
            patch[field.FieldKey.ToString()] = JsonSerializer.SerializeToElement(value);
    }

    /// <summary>Sets a Table field's rows from column-semantic-keyed dictionaries, resolving each column's
    /// columnKey via the semantic map. Unresolvable columns are simply omitted from that row.</summary>
    private static void SetTable(Dictionary<string, JsonElement> patch, IReadOnlyDictionary<string, SemanticField> semantics, string semanticKey, IEnumerable<Dictionary<string, string>> rows)
    {
        if (!semantics.TryGetValue(semanticKey, out var field) || field.FieldType != FieldType.Table)
            return;

        var mappedRows = new List<Dictionary<string, string>>();
        foreach (var row in rows)
        {
            var mapped = new Dictionary<string, string>();
            foreach (var (columnSemantic, value) in row)
                if (field.Columns.TryGetValue(columnSemantic, out var columnKey))
                    mapped[columnKey.ToString()] = value;
            if (mapped.Count > 0)
                mappedRows.Add(mapped);
        }

        if (mappedRows.Count > 0)
            patch[field.FieldKey.ToString()] = JsonSerializer.SerializeToElement(mappedRows);
    }

    // ================================================================================= Meetings

    private async Task CreateMeetingsAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var meetingStudents = students.Where(s => s.HasIep).Take(6).ToList();
        if (meetingStudents.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var meetingTypes = new[] { MeetingType.AnnualReview, MeetingType.Amendment, MeetingType.InitialIep, MeetingType.Reevaluation, MeetingType.Transition, MeetingType.AnnualReview };

        for (var i = 0; i < meetingStudents.Count; i++)
        {
            var student = meetingStudents[i];
            var startsAt = now.AddDays(2 + i * 5).Date.AddHours(14); // spread across the next ~30 days
            var create = await _meetingService.CreateAsync(actingUserId, student.Id, new CreateMeetingModel
            {
                Type = meetingTypes[i % meetingTypes.Length],
                Title = $"{meetingTypes[i % meetingTypes.Length]} Meeting — {student.FirstName} {student.LastName}",
                StartsAtUtc = startsAt,
                DurationMinutes = 60,
                Location = "Maple Ridge conference room",
                Notes = "Demo seed data."
            }, ct);

            if (!create.Success || create.Data == null)
            {
                _logger.LogWarning("Demo seed: failed to create meeting for student {StudentId}: {Message}", student.Id, create.Message);
                continue;
            }

            if (i == 0)
            {
                var held = await _meetingService.SetStatusAsync(actingUserId, create.Data.Id, MeetingStatus.Held, ct);
                if (held.Success)
                {
                    await _meetingDecisionService.CreateAsync(actingUserId, create.Data.Id, new CreateMeetingDecisionModel
                    {
                        Text = "Team agreed to add specialized reading instruction 4x/week.",
                        Outcome = MeetingDecisionOutcome.Agreed
                    }, ct);
                    await _meetingDecisionService.CreateAsync(actingUserId, create.Data.Id, new CreateMeetingDecisionModel
                    {
                        Text = "Family requested additional data on math progress before the next review.",
                        Outcome = MeetingDecisionOutcome.Deferred
                    }, ct);
                }
            }
        }
    }

    // ================================================================================= Family engagement (parents + student account)

    private async Task CreateFamilyEngagementAsync(int actingUserId, List<DemoStaff> staff, List<DemoStudent> students, List<DemoLoginRow> logins, CancellationToken ct)
    {
        var iepStudents = students.Where(s => s.HasIep).ToList();
        if (iepStudents.Count < 2)
            return;

        // Parent 1: fully engaged — linked, shared revision, 2 responses, 1 acknowledgement.
        var studentA = iepStudents[0];
        const string parent1Email = $"jamie.parent@{EmailDomain}";
        var parent1UserId = await CreateAccountUserAsync(parent1Email, "Jamie", "Rivera", ct);

        var invite1 = await _childLinkService.InviteParentAsync(actingUserId, studentA.Id, parent1Email, ct);
        if (!invite1.Success)
            throw new InvalidOperationException($"Failed to invite parent1: {invite1.Message}");
        var token1 = await ExtractLatestTokenAsync(parent1Email, "SchoolLinkInvite", ct);
        var accept1 = await _childLinkService.AcceptInviteAsync(parent1UserId, token1, null, ct);
        if (!accept1.Success)
            throw new InvalidOperationException($"Failed to accept parent1 link: {accept1.Message}");

        logins.Add(new DemoLoginRow("Parent (linked + engaged)", parent1Email, DemoPassword));

        // studentA only ever had one DocumentInstance created for them (the IEP) — IEP and ETR students
        // are disjoint groups in CreateStudentsAsync. The Draft instance stays Status=Draft even after
        // finalize (AuthoredDocumentVersionService re-opens it), so sharing it shares the finalized values.
        var instanceId = await _context.DocumentInstances
            .Where(d => d.SchoolStudentId == studentA.Id)
            .Select(d => d.Id)
            .FirstAsync(ct);

        var share = await _draftSharingService.ShareAsync(actingUserId, instanceId, "Please review the latest IEP draft and let us know your thoughts.", ct);
        if (share.Success && share.Data != null)
        {
            await _draftResponseService.CreateAsync(parent1UserId, share.Data.Id, new CreateDraftResponseModel
            {
                Kind = DraftResponseKind.Question,
                Text = "Can you say more about how the reading goal will be measured?"
            }, ct);
            await _draftResponseService.CreateAsync(parent1UserId, share.Data.Id, new CreateDraftResponseModel
            {
                Kind = DraftResponseKind.Agree,
                Text = "The math goal looks great — thank you!"
            }, ct);
            await _draftSharingService.AcknowledgeAsync(parent1UserId, share.Data.Id, ct);
        }
        else
        {
            _logger.LogWarning("Demo seed: failed to share draft for student {StudentId}: {Message}", studentA.Id, share.Message);
        }

        // Parent 2: linked only (no engagement) — a different student.
        var studentB = iepStudents[1];
        const string parent2Email = $"morgan.parent@{EmailDomain}";
        var parent2UserId = await CreateAccountUserAsync(parent2Email, "Morgan", "Diaz", ct);

        var invite2 = await _childLinkService.InviteParentAsync(actingUserId, studentB.Id, parent2Email, ct);
        if (!invite2.Success)
            throw new InvalidOperationException($"Failed to invite parent2: {invite2.Message}");
        var token2 = await ExtractLatestTokenAsync(parent2Email, "SchoolLinkInvite", ct);
        var accept2 = await _childLinkService.AcceptInviteAsync(parent2UserId, token2, null, ct);
        if (!accept2.Success)
            throw new InvalidOperationException($"Failed to accept parent2 link: {accept2.Message}");

        logins.Add(new DemoLoginRow("Parent (linked)", parent2Email, DemoPassword));

        // 1 student account: a third student, invited by their case-manager teacher.
        var studentC = iepStudents.Count > 2 ? iepStudents[2] : iepStudents[0];
        const string studentEmail = $"riley.student@{EmailDomain}";
        var studentUserId = await CreateAccountUserAsync(studentEmail, "Riley", "Student", ct);

        var caseManagerUserId = staff.First(s => s.StaffProfileId == studentC.CaseManagerStaffProfileId).UserId;
        var studentInvite = await _studentInviteService.InviteFromEducatorAsync(caseManagerUserId, studentC.Id, studentEmail, ct);
        if (!studentInvite.Success)
            throw new InvalidOperationException($"Failed to invite student account: {studentInvite.Message}");
        var studentToken = await ExtractLatestTokenAsync(studentEmail, "StudentInvite", ct);
        var studentAccept = await _studentInviteService.AcceptInviteAsync(studentUserId, studentToken, consentAccepted: true, ct);
        if (!studentAccept.Success)
            throw new InvalidOperationException($"Failed to accept student invite: {studentAccept.Message}");

        logins.Add(new DemoLoginRow("Student account", studentEmail, DemoPassword));
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

    // ================================================================================= Evaluation case + contact attempts

    private async Task CreateEvaluationCaseAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var student = students.Skip(20).FirstOrDefault() ?? students.LastOrDefault();
        if (student == null)
            return;

        var result = await _evaluationCaseService.CreateAsync(actingUserId, student.Id, new CreateEvaluationCaseModel
        {
            Kind = EvaluationCaseKind.Initial,
            ReferralDate = DateTime.UtcNow.AddDays(-10),
            ReferralSource = "Teacher referral — reading and math concerns raised in RTI meeting."
        }, ct);

        if (!result.Success)
            _logger.LogWarning("Demo seed: failed to create evaluation case for student {StudentId}: {Message}", student.Id, result.Message);
    }

    private async Task CreateContactAttemptsAsync(int actingUserId, List<DemoStudent> students, CancellationToken ct)
    {
        var targets = students.Where(s => s.HasIep).Take(4).ToList();
        var methods = new[] { FamilyContactMethod.Phone, FamilyContactMethod.Email, FamilyContactMethod.Letter, FamilyContactMethod.InPerson };
        var outcomes = new[] { FamilyContactOutcome.Reached, FamilyContactOutcome.LeftMessage, FamilyContactOutcome.NoAnswer, FamilyContactOutcome.Reached };

        for (var i = 0; i < targets.Count; i++)
        {
            var result = await _familyContactService.RecordContactAttemptAsync(actingUserId, targets[i].Id, new CreateFamilyContactAttemptModel
            {
                AttemptedAt = DateTime.UtcNow.AddDays(-(3 + i * 5)),
                Method = methods[i % methods.Length],
                Outcome = outcomes[i % outcomes.Length],
                Note = "Demo seed data — routine check-in ahead of the annual review."
            }, ct);

            if (!result.Success)
                _logger.LogWarning("Demo seed: failed to record contact attempt for student {StudentId}: {Message}", targets[i].Id, result.Message);
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
            var userIdsToDelete = staffUserIds
                .Concat(demoOnlyParentUserIds)
                .Concat(studentAccountUserIds)
                .Concat(strandedDemoEmailUserIds)
                .Distinct()
                .ToList();

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
            await transaction.RollbackAsync(ct);
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
