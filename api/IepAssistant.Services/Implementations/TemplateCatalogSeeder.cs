using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using static IepAssistant.Services.Implementations.TemplateGraphBuilder;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Seeds the launch-state template catalog on top of the default IEP template:
/// <list type="bullet">
/// <item><b>OH IEP</b> — section set of Ohio's PR-07 (2026-27 form family).</item>
/// <item><b>OH ETR</b> — section set of Ohio's PR-06 Evaluation Team Report.</item>
/// <item><b>Default Section 504</b> — state-less 504 plan so the third document type resolves.</item>
/// </list>
/// Each template is idempotent on (StateCode, DocumentTypeId) and inserted as ONE graph (template →
/// Published v1 → sections → fields) in a single SaveChanges, mirroring <see cref="DefaultIepTemplateSeeder"/>.
/// Structure fidelity (section names, order, structured goal/service blocks with semantics) is the goal
/// here; PDF form layout is handled by the renderer.
/// </summary>
public sealed class TemplateCatalogSeeder : ITemplateCatalogSeeder
{
    public const string OhioStateCode = "OH";
    public const string OhioIepName = "Ohio IEP (PR-07, 2026-27)";
    public const string OhioEtrName = "Ohio Evaluation Team Report (PR-06, 2026-27)";
    public const string Default504Name = "Default Section 504 Plan";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<TemplateCatalogSeeder> _logger;

    public TemplateCatalogSeeder(ApplicationDbContext context, ILogger<TemplateCatalogSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TemplateCatalogSeedResult> SeedAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Template catalog seed starting");
        var types = await _context.DocumentTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.Key, t => t.Id, StringComparer.Ordinal, ct);

        var created = new List<string>();
        var skipped = new List<string>();

        foreach (var def in Catalog())
        {
            if (!types.TryGetValue(def.DocumentTypeKey, out var typeId))
            {
                _logger.LogWarning("Template catalog seed skipped '{Name}': no '{Key}' document type row.", def.Name, def.DocumentTypeKey);
                skipped.Add(def.Name);
                continue;
            }

            var exists = await _context.DocumentTemplates.AsNoTracking()
                .AnyAsync(t => t.DocumentTypeId == typeId && t.StateCode == def.StateCode, ct);
            if (exists)
            {
                skipped.Add(def.Name);
                continue;
            }
            _logger.LogInformation("Template catalog seeding '{Name}'", def.Name);

            try
            {
                var now = DateTime.UtcNow;
                var version = BuildPublishedVersion(1, def.Sections, now);
                var template = new DocumentTemplate
                {
                    StateCode = def.StateCode,
                    DocumentTypeId = typeId,
                    Name = def.Name,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Versions = { version }
                };
                _context.DocumentTemplates.Add(template);
                await _context.SaveChangesAsync(ct);
                created.Add(def.Name);
                _logger.LogInformation("Seeded template '{Name}' (Published v1 {VersionId}).", def.Name, version.Id);
            }
            catch (DbUpdateException ex)
            {
                // Unique (StateCode, DocumentTypeId) rejected a concurrent insert — confirm and treat as no-op.
                _context.ChangeTracker.Clear();
                var seededByOther = await _context.DocumentTemplates.AsNoTracking()
                    .AnyAsync(t => t.DocumentTypeId == typeId && t.StateCode == def.StateCode, ct);
                if (!seededByOther)
                    throw;
                _logger.LogInformation(ex, "Template '{Name}' seeded concurrently; treating as no-op.", def.Name);
                skipped.Add(def.Name);
            }
        }

        return new TemplateCatalogSeedResult(created, skipped);
    }

    // ---------------------------------------------------------------- Catalog

    private sealed record TemplateDef(string? StateCode, string DocumentTypeKey, string Name, IReadOnlyList<SectionSpec> Sections);

    private static IEnumerable<TemplateDef> Catalog()
    {
        yield return new TemplateDef(OhioStateCode, "IEP", OhioIepName, OhioIepSections());
        yield return new TemplateDef(OhioStateCode, "ETR", OhioEtrName, OhioEtrSections());
        yield return new TemplateDef(null, "Section504", Default504Name, Default504Sections());
    }

    // Template ids for deterministic keys.
    private const byte OhIep = 0x0a;
    private const byte OhEtr = 0x0b;
    private const byte Def504 = 0x0c;

    private static FieldSpec Rich(byte t, byte s, byte f, string label, string? semantic = null, bool required = false)
        => new(Key(t, s, f), FieldType.RichText, label, RichTextConfig(semantic), required);
    private static FieldSpec Text(byte t, byte s, byte f, string label, string? semantic = null, bool required = false)
        => new(Key(t, s, f), FieldType.Text, label, TextConfig(semantic), required);
    private static FieldSpec Date(byte t, byte s, byte f, string label, string? semantic = null, bool required = false)
        => new(Key(t, s, f), FieldType.Date, label, DateConfig(semantic), required);
    private static FieldSpec Check(byte t, byte s, byte f, string label, string? semantic = null)
        => new(Key(t, s, f), FieldType.Checkbox, label, CheckboxConfig(semantic));
    private static FieldSpec Select(byte t, byte s, byte f, string label, string? semantic, params string[] options)
        => new(Key(t, s, f), FieldType.Select, label, SelectConfig(semantic, options));

    private static FieldSpec GoalsTable(byte t, byte s, byte f, string label) => new(Key(t, s, f), FieldType.Table, label,
        TableConfig(FieldSemantics.Goals,
            (Key(t, s, f, 1), FieldType.Text, "Area / Domain",           ColumnSemantics.Domain),
            (Key(t, s, f, 2), FieldType.Text, "Measurable annual goal",  ColumnSemantics.GoalText),
            (Key(t, s, f, 3), FieldType.Text, "Present level / baseline", ColumnSemantics.Baseline),
            (Key(t, s, f, 4), FieldType.Text, "Criteria for mastery",    ColumnSemantics.TargetCriteria),
            (Key(t, s, f, 5), FieldType.Text, "Method of measurement",   ColumnSemantics.MeasurementMethod),
            (Key(t, s, f, 6), FieldType.Text, "Timeframe / reporting",   ColumnSemantics.Timeframe)));

    private static FieldSpec ServicesTable(byte t, byte s, byte f, string label) => new(Key(t, s, f), FieldType.Table, label,
        TableConfig(FieldSemantics.Services,
            (Key(t, s, f, 1), FieldType.Text, "Service",              ColumnSemantics.ServiceType),
            (Key(t, s, f, 2), FieldType.Text, "Provider title/role",  ColumnSemantics.ProviderRole),
            (Key(t, s, f, 3), FieldType.Text, "Location of service",  ColumnSemantics.Location),
            (Key(t, s, f, 4), FieldType.Date, "Begin date",           ColumnSemantics.StartDate),
            (Key(t, s, f, 5), FieldType.Date, "End date",             ColumnSemantics.EndDate),
            (Key(t, s, f, 6), FieldType.Text, "Amount of time",       ColumnSemantics.Duration),
            (Key(t, s, f, 7), FieldType.Text, "Frequency",            ColumnSemantics.Frequency)));

    private static FieldSpec AccommodationsTable(byte t, byte s, byte f, string label) => new(Key(t, s, f), FieldType.Table, label,
        TableConfig(FieldSemantics.Accommodations,
            (Key(t, s, f, 1), FieldType.Text, "Category",       ColumnSemantics.Category),
            (Key(t, s, f, 2), FieldType.Text, "Accommodation",  ColumnSemantics.Accommodation),
            (Key(t, s, f, 3), FieldType.Text, "Setting / when", null)));

    private static FieldSpec ParticipantsTable(byte t, byte s, byte f) => new(Key(t, s, f), FieldType.Table, "Meeting participants",
        TableConfig(FieldSemantics.Participants,
            (Key(t, s, f, 1), FieldType.Text, "Name", ColumnSemantics.ParticipantName),
            (Key(t, s, f, 2), FieldType.Text, "Role", ColumnSemantics.ParticipantRole),
            (Key(t, s, f, 3), FieldType.Checkbox, "Attended", ColumnSemantics.Attended)));

    // ---------------------------------------------------------------- Ohio IEP (PR-07)

    private static IReadOnlyList<SectionSpec> OhioIepSections()
    {
        const byte T = OhIep;
        return new List<SectionSpec>
        {
            new(Key(T, 1), "Student and Meeting Information", new[]
            {
                Rich(T, 1, 1, "Student profile (name, DOB, grade, district, disability category)", FieldSemantics.StudentProfile),
                Select(T, 1, 2, "Meeting type", null, "Initial IEP", "Annual Review", "Amendment", "Review Other Than Annual", "Transition"),
                Date(T, 1, 3, "IEP meeting date", FieldSemantics.MeetingDate),
                Date(T, 1, 4, "IEP effective start date", FieldSemantics.EffectiveDates),
                Date(T, 1, 5, "IEP effective end date"),
                Date(T, 1, 6, "Next annual review due"),
                Date(T, 1, 7, "Next re-evaluation (ETR) due"),
            }),
            new(Key(T, 2), "Section 1: Future Planning", new[]
            {
                Rich(T, 2, 1, "Future planning — student, family and team vision", FieldSemantics.FuturePlanning),
            }),
            new(Key(T, 3), "Section 2: Special Instructional Factors", new[]
            {
                Check(T, 3, 1, "Behavior impedes learning of self or others"),
                Check(T, 3, 2, "Limited English proficiency"),
                Check(T, 3, 3, "Blind or visually impaired"),
                Check(T, 3, 4, "Communication needs"),
                Check(T, 3, 5, "Deaf or hard of hearing"),
                Check(T, 3, 6, "Assistive technology devices or services"),
                Rich(T, 3, 7, "How the special factors are addressed", FieldSemantics.SpecialFactors),
            }),
            new(Key(T, 4), "Section 3: Profile", new[]
            {
                Rich(T, 4, 1, "Present levels of academic achievement and functional performance", FieldSemantics.PresentLevels),
                Rich(T, 4, 2, "Parent and student concerns / input"),
                Rich(T, 4, 3, "Summary of most recent evaluation (ETR) and progress data", FieldSemantics.Eligibility),
            }),
            new(Key(T, 5), "Section 4: Extended School Year Services", new[]
            {
                Select(T, 5, 1, "ESY determination", FieldSemantics.ExtendedSchoolYear, "Not needed", "Needed", "To be determined"),
                Rich(T, 5, 2, "ESY rationale and services"),
            }),
            new(Key(T, 6), "Section 5: Postsecondary Transition (age 14+)", new[]
            {
                Rich(T, 6, 1, "Age-appropriate transition assessments and results"),
                new FieldSpec(Key(T, 6, 2), FieldType.Table, "Postsecondary goals and transition services", TableConfig(FieldSemantics.Transition,
                    (Key(T, 6, 2, 1), FieldType.Text, "Postsecondary goal area (training/education, employment, independent living)", ColumnSemantics.GoalArea),
                    (Key(T, 6, 2, 2), FieldType.Text, "Measurable postsecondary goal", null),
                    (Key(T, 6, 2, 3), FieldType.Text, "Transition services / activities", ColumnSemantics.TransitionServices),
                    (Key(T, 6, 2, 4), FieldType.Text, "Responsible agency / person", null))),
                Rich(T, 6, 3, "Course of study"),
            }),
            new(Key(T, 7), "Section 6: Measurable Annual Goals", new[]
            {
                GoalsTable(T, 7, 1, "Measurable annual goals"),
                Rich(T, 7, 2, "Progress reporting — how and when progress toward goals is reported to parents", FieldSemantics.ProgressMonitoring),
            }),
            new(Key(T, 8), "Section 7: Specially Designed Services", new[]
            {
                ServicesTable(T, 8, 1, "Specially designed instruction and related services"),
                AccommodationsTable(T, 8, 2, "Accommodations"),
                Rich(T, 8, 3, "Modifications"),
                Rich(T, 8, 4, "Support for school personnel"),
                Rich(T, 8, 5, "Service(s) to support medical needs"),
            }),
            new(Key(T, 9), "Section 8: Transportation", new[]
            {
                Select(T, 9, 1, "Transportation as a related service", FieldSemantics.Transportation, "Not needed", "Needed"),
                Rich(T, 9, 2, "Transportation details"),
            }),
            new(Key(T, 10), "Section 9: Nonacademic and Extracurricular Activities", new[]
            {
                Rich(T, 10, 1, "Participation in nonacademic and extracurricular activities"),
            }),
            new(Key(T, 11), "Section 10: General Factors", new[]
            {
                Rich(T, 11, 1, "General factors considered (strengths, concerns, evaluation results, needs)"),
            }),
            new(Key(T, 12), "Section 11: Least Restrictive Environment", new[]
            {
                Rich(T, 12, 1, "Placement decision and justification", FieldSemantics.Lre),
                Rich(T, 12, 2, "Extent of nonparticipation with nondisabled peers", FieldSemantics.Placement),
            }),
            new(Key(T, 13), "Section 12: Statewide and District Wide Testing", new[]
            {
                Rich(T, 13, 1, "Assessment participation and accommodations", FieldSemantics.Testing),
                Check(T, 13, 2, "Alternate assessment (AASCD) — criteria met"),
            }),
            new(Key(T, 14), "Section 13: Meeting Participants", new[]
            {
                ParticipantsTable(T, 14, 1),
            }),
            new(Key(T, 15), "Section 14: Signatures", new[]
            {
                Rich(T, 15, 1, "Parent/guardian consent and signature notes", FieldSemantics.Signatures),
                Check(T, 15, 2, "Parent received a copy of procedural safeguards"),
                Check(T, 15, 3, "Parent received a copy of the IEP"),
            }),
        };
    }

    // ---------------------------------------------------------------- Ohio ETR (PR-06)

    private static IReadOnlyList<SectionSpec> OhioEtrSections()
    {
        const byte T = OhEtr;
        return new List<SectionSpec>
        {
            new(Key(T, 1), "Student and Evaluation Information", new[]
            {
                Rich(T, 1, 1, "Student profile (name, DOB, grade, district)", FieldSemantics.StudentProfile),
                Select(T, 1, 2, "Evaluation type", null, "Initial Evaluation", "Reevaluation"),
                Date(T, 1, 3, "Date of referral"),
                Date(T, 1, 4, "Date parent consent received"),
                Date(T, 1, 5, "Evaluation team meeting date", FieldSemantics.MeetingDate),
                Date(T, 1, 6, "Next re-evaluation due"),
            }),
            new(Key(T, 2), "Referral and Planning", new[]
            {
                Rich(T, 2, 1, "Reason for referral and areas of concern", FieldSemantics.ReferralReason),
                Rich(T, 2, 2, "Evaluation planning — areas assessed, existing data reviewed", FieldSemantics.EvaluationPlan),
            }),
            new(Key(T, 3), "Part 1: Individual Evaluator Assessments", new[]
            {
                new FieldSpec(Key(T, 3, 1), FieldType.Table, "Evaluator assessments", TableConfig(FieldSemantics.EvaluatorReports,
                    (Key(T, 3, 1, 1), FieldType.Text, "Area assessed",                       ColumnSemantics.EvaluationDomain),
                    (Key(T, 3, 1, 2), FieldType.Text, "Evaluator (name, title)",             ColumnSemantics.EvaluatorName),
                    (Key(T, 3, 1, 3), FieldType.Text, "Summary of assessment results",       ColumnSemantics.Findings),
                    (Key(T, 3, 1, 4), FieldType.Text, "Description of educational needs",    null),
                    (Key(T, 3, 1, 5), FieldType.Text, "Implications for instruction and progress monitoring", null))),
            }),
            new(Key(T, 4), "Part 2: Team Summary", new[]
            {
                Rich(T, 4, 1, "Summary of assessment results", FieldSemantics.TeamSummary),
                Rich(T, 4, 2, "Description of educational needs", FieldSemantics.PresentLevels),
                Rich(T, 4, 3, "Implications for instruction and progress monitoring"),
            }),
            new(Key(T, 5), "Part 3: Eligibility Determination", new[]
            {
                Select(T, 5, 1, "Eligibility determination", FieldSemantics.EligibilityDetermination, "Eligible", "Not eligible"),
                Select(T, 5, 2, "Disability category", FieldSemantics.Eligibility,
                    "Autism", "Deaf-Blindness", "Deafness (Hearing Impairment)", "Emotional Disturbance", "Intellectual Disability",
                    "Multiple Disabilities", "Orthopedic Impairment", "Other Health Impairment", "Specific Learning Disability",
                    "Speech or Language Impairment", "Traumatic Brain Injury", "Visual Impairment", "Developmental Delay"),
                Rich(T, 5, 3, "Documentation of eligibility criteria and basis for determination"),
                Check(T, 5, 4, "Adverse effect on educational performance"),
                Check(T, 5, 5, "Need for specially designed instruction"),
                Check(T, 5, 6, "Determination is not primarily the result of lack of instruction or limited English proficiency"),
            }),
            new(Key(T, 6), "Part 4: Evaluation Team", new[]
            {
                ParticipantsTable(T, 6, 1),
                Rich(T, 6, 2, "Team member disagreement statements (if any)"),
                Check(T, 6, 3, "Parent received a copy of the ETR"),
                Check(T, 6, 4, "Parent received a copy of procedural safeguards"),
            }),
        };
    }

    // ---------------------------------------------------------------- Default Section 504

    private static IReadOnlyList<SectionSpec> Default504Sections()
    {
        const byte T = Def504;
        return new List<SectionSpec>
        {
            new(Key(T, 1), "Student Information", new[]
            {
                Rich(T, 1, 1, "Student profile", FieldSemantics.StudentProfile),
                Date(T, 1, 2, "Plan meeting date", FieldSemantics.MeetingDate),
                Date(T, 1, 3, "Plan review date"),
            }),
            new(Key(T, 2), "Basis for Eligibility", new[]
            {
                Rich(T, 2, 1, "Physical or mental impairment", FieldSemantics.Eligibility),
                Rich(T, 2, 2, "Major life activity substantially limited"),
                Rich(T, 2, 3, "Evaluation data and sources reviewed", FieldSemantics.PresentLevels),
            }),
            new(Key(T, 3), "Accommodations and Services", new[]
            {
                AccommodationsTable(T, 3, 1, "Accommodations"),
                ServicesTable(T, 3, 2, "Related aids and services"),
            }),
            new(Key(T, 4), "Participants and Signatures", new[]
            {
                ParticipantsTable(T, 4, 1),
                Check(T, 4, 2, "Parent received notice of Section 504 rights"),
            }),
        };
    }
}
