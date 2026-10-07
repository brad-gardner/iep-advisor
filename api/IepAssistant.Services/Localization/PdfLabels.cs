using Microsoft.Extensions.Localization;

namespace IepAssistant.Services.Localization;

/// <summary>
/// Multilingual plan (2026-10-06) phase 7: every app-generated label/heading/footer string
/// <c>IepVersionPdfDocument</c> and <c>AuthoredDocumentPdfDocument</c> print, resolved ONCE by the render
/// service (<c>IepVersionPdfService</c>/<c>AuthoredDocumentPdfService</c>) from <c>IStringLocalizer&lt;Pdf&gt;</c>
/// and passed into the document's constructor as plain data. This keeps the QuestPDF document classes
/// themselves dependency-injection-free — consistent with their documented "pure layout — no I/O, no DB
/// access" contract — while still letting them render in either language.
///
/// <para>Every property's default value is the EXACT pre-phase-7 English literal that call site used, so
/// <c>new PdfLabels()</c> (used directly by every existing test that constructs a document without an
/// explicit labels argument) reproduces byte-identical English output. <see cref="From"/> builds the same
/// values from <c>Resources/Pdf.resx</c> — which must therefore hold the IDENTICAL English text — so a
/// render service resolving English through the localizer is indistinguishable from the bare default.</para>
///
/// <para>Format-string properties (e.g. <see cref="Version"/>, <see cref="Responsible"/>) carry a numeric
/// <c>{0}</c>/<c>{1}</c> placeholder applied with <see cref="string.Format(string, object?)"/> at the call
/// site — never interpolated here. <see cref="PagePrefix"/>/<see cref="PageOfMiddle"/> are the one
/// exception kept as two plain fragments rather than one "{0} of {1}" template: QuestPDF's
/// <c>CurrentPageNumber()</c>/<c>TotalPages()</c> are live layout widgets composed BETWEEN these spans,
/// not numbers known at format time.</para>
/// </summary>
public sealed record PdfLabels(
    string PagePrefix = "Page ",
    string PageOfMiddle = " of ",
    string Version = "Version {0}",
    string Finalized = "Finalized: {0}",
    string Effective = "Effective: {0}",

    // IepVersionPdfDocument (legacy P5a layout)
    string PresentLevelsAndNarrative = "Present Levels & Narrative",
    string Goals = "Goals",
    string Goal = "Goal",
    string Baseline = "Baseline",
    string TargetCriteria = "Target Criteria",
    string Measurement = "Measurement",
    string Timeframe = "Timeframe",
    string Services = "Services",
    string Service = "Service",
    string Frequency = "Frequency",
    string Duration = "Duration",
    string Location = "Location",
    string Provider = "Provider",
    string Dates = "Dates",
    string Accommodations = "Accommodations",
    string Transition = "Transition",

    // AuthoredDocumentPdfDocument (State Document Template Engine: generic + OH form layouts)
    string Document = "Document",
    string NotAddressed = "Not addressed",
    string Objectives = "Objectives",
    string Participants = "Participants",
    string Attended = "Attended",
    string DidNotAttend = "Did not attend",
    string AttendanceNotRecorded = "Attendance not recorded",
    string Signatures = "Signatures",
    string ParentGuardian = "Parent/Guardian",
    string Student = "Student",
    string DistrictRepresentative = "District Representative",
    string Teacher = "Teacher",
    string NameFieldLine = "Name: _______________________",
    string SignatureFieldLine = "Signature: _______________________",
    string DateFieldLine = "Date: __________",
    string Responsible = "Responsible: {0}",
    string ResponsibleHeader = "Responsible",
    string Yes = "Yes",
    string No = "No",
    string StudentLine = "Student: {0}",
    string DateOfBirthLine = "Date of birth: {0}",
    string DistrictLine = "District: {0}",
    string IepEtrDateLine = "IEP date: {0}   ETR date: {1}",
    string MeetingDateLine = "Meeting date: {0}",
    string VersionFormVersionLine = "Version {0} — Form version: template v{1}",
    string OhioForm = "Ohio Department of Education Form {0}",
    string AmendmentTo = "Amendment to v{0}",
    string EffectiveSuffix = " — effective {0}")
{
    /// <summary>The English defaults above, named for call sites that want to be explicit rather than
    /// relying on <c>new PdfLabels()</c>.</summary>
    public static readonly PdfLabels English = new();

    /// <summary>
    /// Resolves every label from <paramref name="localizer"/>. Called once per render by the PDF render
    /// service, inside the same <see cref="CultureScope"/> the render runs under, so the localizer's
    /// current-UI-culture lookup matches the language being rendered.
    /// </summary>
    public static PdfLabels From(IStringLocalizer<Pdf> localizer) => new(
        PagePrefix: localizer["Pdf.PagePrefix"],
        PageOfMiddle: localizer["Pdf.PageOfMiddle"],
        Version: localizer["Pdf.Version"],
        Finalized: localizer["Pdf.Finalized"],
        Effective: localizer["Pdf.Effective"],

        PresentLevelsAndNarrative: localizer["Pdf.PresentLevelsAndNarrative"],
        Goals: localizer["Pdf.Goals"],
        Goal: localizer["Pdf.Goal"],
        Baseline: localizer["Pdf.Baseline"],
        TargetCriteria: localizer["Pdf.TargetCriteria"],
        Measurement: localizer["Pdf.Measurement"],
        Timeframe: localizer["Pdf.Timeframe"],
        Services: localizer["Pdf.Services"],
        Service: localizer["Pdf.Service"],
        Frequency: localizer["Pdf.Frequency"],
        Duration: localizer["Pdf.Duration"],
        Location: localizer["Pdf.Location"],
        Provider: localizer["Pdf.Provider"],
        Dates: localizer["Pdf.Dates"],
        Accommodations: localizer["Pdf.Accommodations"],
        Transition: localizer["Pdf.Transition"],

        Document: localizer["Pdf.Document"],
        NotAddressed: localizer["Pdf.NotAddressed"],
        Objectives: localizer["Pdf.Objectives"],
        Participants: localizer["Pdf.Participants"],
        Attended: localizer["Pdf.Attended"],
        DidNotAttend: localizer["Pdf.DidNotAttend"],
        AttendanceNotRecorded: localizer["Pdf.AttendanceNotRecorded"],
        Signatures: localizer["Pdf.Signatures"],
        ParentGuardian: localizer["Pdf.ParentGuardian"],
        Student: localizer["Pdf.Student"],
        DistrictRepresentative: localizer["Pdf.DistrictRepresentative"],
        Teacher: localizer["Pdf.Teacher"],
        NameFieldLine: localizer["Pdf.NameFieldLine"],
        SignatureFieldLine: localizer["Pdf.SignatureFieldLine"],
        DateFieldLine: localizer["Pdf.DateFieldLine"],
        Responsible: localizer["Pdf.Responsible"],
        ResponsibleHeader: localizer["Pdf.ResponsibleHeader"],
        Yes: localizer["Pdf.Yes"],
        No: localizer["Pdf.No"],
        StudentLine: localizer["Pdf.StudentLine"],
        DateOfBirthLine: localizer["Pdf.DateOfBirthLine"],
        DistrictLine: localizer["Pdf.DistrictLine"],
        IepEtrDateLine: localizer["Pdf.IepEtrDateLine"],
        MeetingDateLine: localizer["Pdf.MeetingDateLine"],
        VersionFormVersionLine: localizer["Pdf.VersionFormVersionLine"],
        OhioForm: localizer["Pdf.OhioForm"],
        AmendmentTo: localizer["Pdf.AmendmentTo"],
        EffectiveSuffix: localizer["Pdf.EffectiveSuffix"]);
}
