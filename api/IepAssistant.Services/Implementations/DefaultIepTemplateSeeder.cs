using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Seeds the DEFAULT (state-less) IEP template that reproduces the legacy typed IEP structure so new IEP
/// drafts resolve through the generic template engine (Phase 5). See <see cref="IDefaultIepTemplateSeeder"/>.
///
/// <para><b>Why a runtime seeder (not a HasData migration):</b> the flat lookup tables (DocumentType,
/// OrgRole) use HasData, but this seed is a multi-row tree (template → published version → 10 sections →
/// 10 fields, several carrying GUID-keyed Table ConfigJson). HasData would need fixed integer PKs on
/// every row plus hand-authored ConfigJson snapshots and would churn the model snapshot; a runtime seeder
/// lets the DB assign integer PKs while we keep <em>stable GUIDs</em> for SectionKey/FieldKey/ColumnKey so
/// re-seeding and future version forks stay stable. It mirrors the repo's existing
/// <c>AnalysisRunBackfillHostedService</c> startup-seeder pattern.</para>
///
/// <para><b>Idempotency:</b> the run is guarded by a pre-check for an existing default IEP template
/// (StateCode == null &amp;&amp; DocumentTypeId == IEP), and the DB unique index on
/// (StateCode, DocumentTypeId) is the concurrency backstop — a lost race surfaces as a
/// <see cref="DbUpdateException"/> that we treat as "already seeded".</para>
///
/// <para><b>Structure &amp; required flags:</b> six narrative sections (each a single RichText field,
/// mirroring <c>IepDraftSection.RichText</c> per <see cref="IepSectionKind"/>) followed by Goals,
/// Services, Accommodations and Transition, each a single Table field — matching the section order in
/// <c>IepVersionPdfDocument</c>. Every field and table column is <c>Required = false</c> and no table has
/// row bounds, so partial drafts save cleanly — matching the legacy freeform typed editor, which enforced
/// no required fields.</para>
/// </summary>
public sealed class DefaultIepTemplateSeeder : IDefaultIepTemplateSeeder
{
    /// <summary>Lookup key of the IEP document-type row (seeded by DocumentTypeConfiguration).</summary>
    private const string IepDocumentTypeKey = "IEP";

    /// <summary>Name of the seeded default template (surfaced in the admin template list).</summary>
    public const string DefaultTemplateName = "Default IEP";

    private static readonly JsonSerializerOptions ConfigJsonOptions = TemplateFieldConfigValidator.JsonOptions;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<DefaultIepTemplateSeeder> _logger;

    public DefaultIepTemplateSeeder(ApplicationDbContext context, ILogger<DefaultIepTemplateSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DefaultIepTemplateSeedResult> SeedAsync(CancellationToken ct = default)
    {
        var iepTypeId = await _context.DocumentTypes.AsNoTracking()
            .Where(t => t.Key == IepDocumentTypeKey)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        if (iepTypeId == null)
        {
            _logger.LogWarning(
                "Default IEP template seed skipped: no '{Key}' document-type row found (migrations not applied?).",
                IepDocumentTypeKey);
            return new DefaultIepTemplateSeedResult(DefaultIepTemplateSeedOutcome.SkippedNoDocumentType);
        }

        // Idempotency pre-check: a default (state-less) IEP template already covers this.
        var existing = await _context.DocumentTemplates.AsNoTracking()
            .Where(t => t.StateCode == null && t.DocumentTypeId == iepTypeId.Value)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
            return new DefaultIepTemplateSeedResult(DefaultIepTemplateSeedOutcome.AlreadySeeded);

        try
        {
            var versionId = await CreateDefaultTemplateAsync(iepTypeId.Value, ct);
            _logger.LogInformation(
                "Seeded default IEP template (Published version {VersionId}) reproducing the legacy typed IEP structure.",
                versionId);
            return new DefaultIepTemplateSeedResult(DefaultIepTemplateSeedOutcome.Created, versionId);
        }
        catch (DbUpdateException ex)
        {
            // The whole graph is inserted in one SaveChanges (one transaction), so a failure rolls back
            // fully — never a partial seed. The expected failure is the (StateCode, DocumentTypeId) unique
            // index rejecting a concurrent instance's duplicate insert. Confirm the default now exists
            // before declaring it a benign race; otherwise this was a genuine write failure — rethrow so
            // the hosted service logs an error and retries on the next boot.
            var seededByOther = await _context.DocumentTemplates.AsNoTracking()
                .AnyAsync(t => t.StateCode == null && t.DocumentTypeId == iepTypeId.Value, ct);
            if (!seededByOther)
                throw;

            _logger.LogInformation(ex,
                "Default IEP template already seeded by a concurrent instance; treating as no-op.");
            return new DefaultIepTemplateSeedResult(DefaultIepTemplateSeedOutcome.AlreadySeeded);
        }
    }

    private async Task<int> CreateDefaultTemplateAsync(int iepTypeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Build the whole graph and insert it in a SINGLE SaveChanges (one transaction) so the seed is
        // atomic — a crash or failure can never leave a Published-but-empty template that the idempotency
        // pre-check would then treat as done. EF fixup assigns the version's integer PK and populates the
        // denormalized TemplateField.DocumentTemplateVersionId from the Version navigation set on each
        // field (and TemplateSection.DocumentTemplateVersionId from the version's Sections collection).
        // All rows are inserted (Added), which ImmutableVersionInterceptor permits even for a Published
        // version (only Modified/Deleted of a Published version is frozen).
        var version = new DocumentTemplateVersion
        {
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            PublishedAt = now,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedAt = now,
            UpdatedAt = now
        };
        foreach (var section in BuildSections(version, now))
            version.Sections.Add(section);

        var template = new DocumentTemplate
        {
            StateCode = null,
            DocumentTypeId = iepTypeId,
            Name = DefaultTemplateName,
            CreatedAt = now,
            UpdatedAt = now,
            Versions = { version }
        };

        _context.DocumentTemplates.Add(template);
        await _context.SaveChangesAsync(ct);

        return version.Id;
    }

    // ---------------------------------------------------------------- Section/field construction

    private static List<TemplateSection> BuildSections(DocumentTemplateVersion version, DateTime now)
    {
        var sections = new List<TemplateSection>();

        // Narrative sections: one RichText field each, mirroring IepDraftSection.RichText per kind. The
        // field label matches the section title (each section has exactly one narrative field). Order
        // matches IepVersionPdfDocument (narrative first, then Goals/Services/Accommodations/Transition).
        var narratives = new (Guid SectionKey, Guid FieldKey, string Title)[]
        {
            (Keys.StudentProfileSection,    Keys.StudentProfileField,    "Student Profile"),
            (Keys.PresentLevelsSection,     Keys.PresentLevelsField,     "Present Levels"),
            (Keys.EligibilitySection,       Keys.EligibilityField,       "Eligibility"),
            (Keys.PlacementSection,         Keys.PlacementField,         "Placement"),
            (Keys.ProgressMonitoringSection,Keys.ProgressMonitoringField,"Progress Monitoring"),
            (Keys.SpecialFactorsSection,    Keys.SpecialFactorsField,    "Special Factors"),
        };

        var order = 0;
        foreach (var (sectionKey, fieldKey, title) in narratives)
        {
            sections.Add(Section(sectionKey, title, order, now,
                Field(version, fieldKey, FieldType.RichText, title, order: 0, config: null, now)));
            order++;
        }

        // Goals table.
        sections.Add(Section(Keys.GoalsSection, "Goals", order++, now,
            Field(version, Keys.GoalsTableField, FieldType.Table, "Goals", order: 0,
                config: TableConfig(
                    (Keys.GoalsDomainColumn,        FieldType.Text, "Domain"),
                    (Keys.GoalsGoalColumn,          FieldType.Text, "Goal"),
                    (Keys.GoalsBaselineColumn,      FieldType.Text, "Baseline"),
                    (Keys.GoalsTargetCriteriaColumn,FieldType.Text, "Target Criteria"),
                    (Keys.GoalsMeasurementColumn,   FieldType.Text, "Measurement Method"),
                    (Keys.GoalsTimeframeColumn,     FieldType.Text, "Timeframe")),
                now)));

        // Services table.
        sections.Add(Section(Keys.ServicesSection, "Services", order++, now,
            Field(version, Keys.ServicesTableField, FieldType.Table, "Service lines", order: 0,
                config: TableConfig(
                    (Keys.ServicesTypeColumn,      FieldType.Text, "Service Type"),
                    (Keys.ServicesFrequencyColumn, FieldType.Text, "Frequency"),
                    (Keys.ServicesDurationColumn,  FieldType.Text, "Duration"),
                    (Keys.ServicesLocationColumn,  FieldType.Text, "Location"),
                    (Keys.ServicesProviderColumn,  FieldType.Text, "Provider Role"),
                    (Keys.ServicesStartDateColumn, FieldType.Date, "Start Date"),
                    (Keys.ServicesEndDateColumn,   FieldType.Date, "End Date")),
                now)));

        // Accommodations table.
        sections.Add(Section(Keys.AccommodationsSection, "Accommodations", order++, now,
            Field(version, Keys.AccommodationsTableField, FieldType.Table, "Accommodations", order: 0,
                config: TableConfig(
                    (Keys.AccommodationsCategoryColumn, FieldType.Text, "Category"),
                    (Keys.AccommodationsTextColumn,     FieldType.Text, "Accommodation")),
                now)));

        // Transition table.
        sections.Add(Section(Keys.TransitionSection, "Transition", order, now,
            Field(version, Keys.TransitionTableField, FieldType.Table, "Transition", order: 0,
                config: TableConfig(
                    (Keys.TransitionGoalAreaColumn, FieldType.Text, "Postsecondary Goal Area"),
                    (Keys.TransitionServicesColumn, FieldType.Text, "Services")),
                now)));

        return sections;
    }

    // The section is attached to the version via the version's Sections collection by the caller, which
    // sets TemplateSection.DocumentTemplateVersionId through EF relationship fixup on save.
    private static TemplateSection Section(
        Guid sectionKey, string title, int displayOrder, DateTime now, TemplateField field)
        => new()
        {
            SectionKey = sectionKey,
            Title = title,
            DisplayOrder = displayOrder,
            CreatedAt = now,
            UpdatedAt = now,
            Fields = { field }
        };

    // Setting the Version navigation populates the denormalized TemplateField.DocumentTemplateVersionId
    // via EF fixup once the version's PK is generated (single-save atomicity). The field is also added to
    // its section's Fields collection by the caller, which sets TemplateSectionId.
    private static TemplateField Field(
        DocumentTemplateVersion version, Guid fieldKey, FieldType type, string label, int order, string? config, DateTime now)
        => new()
        {
            Version = version,
            FieldKey = fieldKey,
            FieldType = type,
            Label = label,
            Required = false, // matches the legacy freeform typed editor (no enforced fields)
            ConfigJson = config,
            DisplayOrder = order,
            CreatedAt = now,
            UpdatedAt = now
        };

    /// <summary>Serializes a Table field's ConfigJson (columns only; no row bounds so partial drafts save).</summary>
    private static string TableConfig(params (Guid ColumnKey, FieldType Type, string Label)[] columns)
    {
        var config = new TableFieldConfig
        {
            Columns = columns
                .Select(c => new TableColumn { ColumnKey = c.ColumnKey, Type = c.Type, Label = c.Label, Required = false })
                .ToList()
        };
        return JsonSerializer.Serialize(config, ConfigJsonOptions);
    }

    /// <summary>
    /// Stable GUIDs for the default IEP template's sections, fields and table columns. These are fixed
    /// once and never change: instance values are keyed by FieldKey/ColumnKey, and version forks carry
    /// these keys verbatim, so re-seeding or re-publishing stays value-stable.
    /// </summary>
    private static class Keys
    {
        // Narrative sections + their single RichText fields.
        public static readonly Guid StudentProfileSection     = new("a1d00000-0000-0000-0000-000000000001");
        public static readonly Guid PresentLevelsSection      = new("a1d00000-0000-0000-0000-000000000002");
        public static readonly Guid EligibilitySection        = new("a1d00000-0000-0000-0000-000000000003");
        public static readonly Guid PlacementSection          = new("a1d00000-0000-0000-0000-000000000004");
        public static readonly Guid ProgressMonitoringSection = new("a1d00000-0000-0000-0000-000000000005");
        public static readonly Guid SpecialFactorsSection     = new("a1d00000-0000-0000-0000-000000000006");
        public static readonly Guid GoalsSection              = new("a1d00000-0000-0000-0000-000000000007");
        public static readonly Guid ServicesSection           = new("a1d00000-0000-0000-0000-000000000008");
        public static readonly Guid AccommodationsSection     = new("a1d00000-0000-0000-0000-000000000009");
        public static readonly Guid TransitionSection         = new("a1d00000-0000-0000-0000-00000000000a");

        public static readonly Guid StudentProfileField     = new("b2f00000-0000-0000-0000-000000000001");
        public static readonly Guid PresentLevelsField      = new("b2f00000-0000-0000-0000-000000000002");
        public static readonly Guid EligibilityField        = new("b2f00000-0000-0000-0000-000000000003");
        public static readonly Guid PlacementField          = new("b2f00000-0000-0000-0000-000000000004");
        public static readonly Guid ProgressMonitoringField = new("b2f00000-0000-0000-0000-000000000005");
        public static readonly Guid SpecialFactorsField     = new("b2f00000-0000-0000-0000-000000000006");
        public static readonly Guid GoalsTableField          = new("b2f00000-0000-0000-0000-000000000007");
        public static readonly Guid ServicesTableField       = new("b2f00000-0000-0000-0000-000000000008");
        public static readonly Guid AccommodationsTableField = new("b2f00000-0000-0000-0000-000000000009");
        public static readonly Guid TransitionTableField     = new("b2f00000-0000-0000-0000-00000000000a");

        // Goals table columns.
        public static readonly Guid GoalsDomainColumn         = new("c3a00000-0000-0000-0000-000000000001");
        public static readonly Guid GoalsGoalColumn           = new("c3a00000-0000-0000-0000-000000000002");
        public static readonly Guid GoalsBaselineColumn       = new("c3a00000-0000-0000-0000-000000000003");
        public static readonly Guid GoalsTargetCriteriaColumn = new("c3a00000-0000-0000-0000-000000000004");
        public static readonly Guid GoalsMeasurementColumn    = new("c3a00000-0000-0000-0000-000000000005");
        public static readonly Guid GoalsTimeframeColumn      = new("c3a00000-0000-0000-0000-000000000006");

        // Services table columns.
        public static readonly Guid ServicesTypeColumn      = new("c4b00000-0000-0000-0000-000000000001");
        public static readonly Guid ServicesFrequencyColumn = new("c4b00000-0000-0000-0000-000000000002");
        public static readonly Guid ServicesDurationColumn  = new("c4b00000-0000-0000-0000-000000000003");
        public static readonly Guid ServicesLocationColumn  = new("c4b00000-0000-0000-0000-000000000004");
        public static readonly Guid ServicesProviderColumn  = new("c4b00000-0000-0000-0000-000000000005");
        public static readonly Guid ServicesStartDateColumn = new("c4b00000-0000-0000-0000-000000000006");
        public static readonly Guid ServicesEndDateColumn   = new("c4b00000-0000-0000-0000-000000000007");

        // Accommodations table columns.
        public static readonly Guid AccommodationsCategoryColumn = new("c5c00000-0000-0000-0000-000000000001");
        public static readonly Guid AccommodationsTextColumn     = new("c5c00000-0000-0000-0000-000000000002");

        // Transition table columns.
        public static readonly Guid TransitionGoalAreaColumn = new("c6d00000-0000-0000-0000-000000000001");
        public static readonly Guid TransitionServicesColumn = new("c6d00000-0000-0000-0000-000000000002");
    }
}
