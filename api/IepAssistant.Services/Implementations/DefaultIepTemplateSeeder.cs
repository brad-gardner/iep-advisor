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
        {
            // A pre-semantics database has the default template at v1 without `semantic` tags. Publish a
            // v2 with the SAME keys plus semantics so new documents pick it up (highest published wins in
            // TemplateResolutionService) while v1-pinned instances keep rendering unchanged.
            var upgradedVersionId = await UpgradeToSemanticVersionIfNeededAsync(existing.Value, ct);
            return upgradedVersionId == null
                ? new DefaultIepTemplateSeedResult(DefaultIepTemplateSeedOutcome.AlreadySeeded)
                : new DefaultIepTemplateSeedResult(DefaultIepTemplateSeedOutcome.Upgraded, upgradedVersionId);
        }

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

    /// <summary>
    /// Publishes a semantic-tagged v(N+1) of an existing default template — but ONLY when the template
    /// is provably the untouched seed: exactly one version, Published, whose FieldKey set equals the
    /// seeder's keys and which carries no semantic tag anywhere. An admin who forked/customized the
    /// default (extra versions, changed keys, or any tagging) keeps full control; we log and leave it.
    /// Returns the new version id, or null when nothing was written.
    /// </summary>
    private async Task<int?> UpgradeToSemanticVersionIfNeededAsync(int templateId, CancellationToken ct)
    {
        var versions = await _context.DocumentTemplateVersions.AsNoTracking()
            .Where(v => v.DocumentTemplateId == templateId)
            .Include(v => v.Sections).ThenInclude(s => s.Fields)
            .AsSplitQuery()
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

        var published = versions.FirstOrDefault(v => v.Status == TemplateVersionStatus.Published);
        if (published == null)
            return null; // no published version at all — never auto-publish a draft

        var fields = published.Sections.SelectMany(s => s.Fields).ToList();
        var anySemantic = fields.Any(f => TemplateSemanticsReader.ReadField(f.FieldType, f.ConfigJson).Semantic != null);
        if (anySemantic)
            return null; // already semantic (or admin-tagged) — nothing to do

        var isPristineSeed = versions.Count == 1
            && fields.Select(f => f.FieldKey).ToHashSet().SetEquals(Keys.AllFieldKeys);
        if (!isPristineSeed)
        {
            _logger.LogWarning(
                "Default IEP template {TemplateId} has been customized (versions={Count}, fields={Fields}); leaving semantic upgrade to administrators. " +
                "Tag the goals/services/accommodations tables via the template builder to enable AI assist and prefill.",
                templateId, versions.Count, fields.Count);
            return null;
        }

        var now = DateTime.UtcNow;
        var version = new DocumentTemplateVersion
        {
            DocumentTemplateId = templateId,
            VersionNumber = published.VersionNumber + 1,
            Status = TemplateVersionStatus.Published,
            PublishedAt = now,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedAt = now,
            UpdatedAt = now
        };
        foreach (var section in BuildSections(version, now))
            version.Sections.Add(section);

        _context.DocumentTemplateVersions.Add(version);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Two instances booting against the same pre-semantic database both compute N+1; the loser
            // hits the unique (DocumentTemplateId, VersionNumber) index. Confirm and treat as a no-op.
            _context.ChangeTracker.Clear();
            var upgradedByOther = await _context.DocumentTemplateVersions.AsNoTracking()
                .AnyAsync(v => v.DocumentTemplateId == templateId && v.VersionNumber == version.VersionNumber, ct);
            if (!upgradedByOther)
                throw;
            _logger.LogInformation(ex, "Default IEP template upgraded concurrently by another instance; treating as no-op.");
            return null;
        }
        _logger.LogInformation("Upgraded default IEP template to semantic version {VersionNumber} ({VersionId}).", version.VersionNumber, version.Id);
        return version.Id;
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
        var narratives = new (Guid SectionKey, Guid FieldKey, string Title, string Semantic)[]
        {
            (Keys.StudentProfileSection,    Keys.StudentProfileField,    "Student Profile",     FieldSemantics.StudentProfile),
            (Keys.PresentLevelsSection,     Keys.PresentLevelsField,     "Present Levels",      FieldSemantics.PresentLevels),
            (Keys.EligibilitySection,       Keys.EligibilityField,       "Eligibility",         FieldSemantics.Eligibility),
            (Keys.PlacementSection,         Keys.PlacementField,         "Placement",           FieldSemantics.Placement),
            (Keys.ProgressMonitoringSection,Keys.ProgressMonitoringField,"Progress Monitoring", FieldSemantics.ProgressMonitoring),
            (Keys.SpecialFactorsSection,    Keys.SpecialFactorsField,    "Special Factors",     FieldSemantics.SpecialFactors),
        };

        var order = 0;
        foreach (var (sectionKey, fieldKey, title, semantic) in narratives)
        {
            sections.Add(Section(sectionKey, title, order, now,
                Field(version, fieldKey, FieldType.RichText, title, order: 0, config: TemplateGraphBuilder.RichTextConfig(semantic), now)));
            order++;
        }

        // Goals table.
        sections.Add(Section(Keys.GoalsSection, "Goals", order++, now,
            Field(version, Keys.GoalsTableField, FieldType.Table, "Goals", order: 0,
                config: TableConfig(FieldSemantics.Goals,
                    (Keys.GoalsDomainColumn,        FieldType.Text, "Domain",             ColumnSemantics.Domain),
                    (Keys.GoalsGoalColumn,          FieldType.Text, "Goal",               ColumnSemantics.GoalText),
                    (Keys.GoalsBaselineColumn,      FieldType.Text, "Baseline",           ColumnSemantics.Baseline),
                    (Keys.GoalsTargetCriteriaColumn,FieldType.Text, "Target Criteria",    ColumnSemantics.TargetCriteria),
                    (Keys.GoalsMeasurementColumn,   FieldType.Text, "Measurement Method", ColumnSemantics.MeasurementMethod),
                    (Keys.GoalsTimeframeColumn,     FieldType.Text, "Timeframe",          ColumnSemantics.Timeframe)),
                now)));

        // Services table.
        sections.Add(Section(Keys.ServicesSection, "Services", order++, now,
            Field(version, Keys.ServicesTableField, FieldType.Table, "Service lines", order: 0,
                config: TableConfig(FieldSemantics.Services,
                    (Keys.ServicesTypeColumn,      FieldType.Text, "Service Type",  ColumnSemantics.ServiceType),
                    (Keys.ServicesFrequencyColumn, FieldType.Text, "Frequency",     ColumnSemantics.Frequency),
                    (Keys.ServicesDurationColumn,  FieldType.Text, "Duration",      ColumnSemantics.Duration),
                    (Keys.ServicesLocationColumn,  FieldType.Text, "Location",      ColumnSemantics.Location),
                    (Keys.ServicesProviderColumn,  FieldType.Text, "Provider Role", ColumnSemantics.ProviderRole),
                    (Keys.ServicesStartDateColumn, FieldType.Date, "Start Date",    ColumnSemantics.StartDate),
                    (Keys.ServicesEndDateColumn,   FieldType.Date, "End Date",      ColumnSemantics.EndDate)),
                now)));

        // Accommodations table.
        sections.Add(Section(Keys.AccommodationsSection, "Accommodations", order++, now,
            Field(version, Keys.AccommodationsTableField, FieldType.Table, "Accommodations", order: 0,
                config: TableConfig(FieldSemantics.Accommodations,
                    (Keys.AccommodationsCategoryColumn, FieldType.Text, "Category",      ColumnSemantics.Category),
                    (Keys.AccommodationsTextColumn,     FieldType.Text, "Accommodation", ColumnSemantics.Accommodation)),
                now)));

        // Transition table.
        sections.Add(Section(Keys.TransitionSection, "Transition", order, now,
            Field(version, Keys.TransitionTableField, FieldType.Table, "Transition", order: 0,
                config: TableConfig(FieldSemantics.Transition,
                    (Keys.TransitionGoalAreaColumn, FieldType.Text, "Postsecondary Goal Area", ColumnSemantics.GoalArea),
                    (Keys.TransitionServicesColumn, FieldType.Text, "Services",                ColumnSemantics.TransitionServices)),
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

    /// <summary>Serializes a Table field's ConfigJson (semantic + columns; no row bounds so partial drafts save).</summary>
    private static string TableConfig(string semantic, params (Guid ColumnKey, FieldType Type, string Label, string? Semantic)[] columns)
        => TemplateGraphBuilder.TableConfig(semantic, columns);

    /// <summary>
    /// Stable GUIDs for the default IEP template's sections, fields and table columns. These are fixed
    /// once and never change: instance values are keyed by FieldKey/ColumnKey, and version forks carry
    /// these keys verbatim, so re-seeding or re-publishing stays value-stable.
    /// </summary>
    public static class Keys
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

        /// <summary>Every FieldKey the seed creates — used to recognize an untouched default template.</summary>
        public static readonly IReadOnlySet<Guid> AllFieldKeys = new HashSet<Guid>
        {
            StudentProfileField, PresentLevelsField, EligibilityField, PlacementField, ProgressMonitoringField,
            SpecialFactorsField, GoalsTableField, ServicesTableField, AccommodationsTableField, TransitionTableField
        };

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
