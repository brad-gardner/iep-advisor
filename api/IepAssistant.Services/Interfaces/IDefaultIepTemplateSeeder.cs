namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Seeds the DEFAULT (state-less) IEP <c>DocumentTemplate</c> that reproduces the legacy typed IEP
/// structure (Phase 5, State Document Template Engine). Runs once at startup and is idempotent — safe to
/// invoke on every boot. This routes new IEP drafts through the generic template engine when no
/// state-specific template exists, without touching the legacy typed IepDraft/IepVersion paths.
/// </summary>
public interface IDefaultIepTemplateSeeder
{
    /// <summary>Ensures the default IEP template exists (a no-op if it already does). Never throws for the "already seeded" race.</summary>
    Task<DefaultIepTemplateSeedResult> SeedAsync(CancellationToken ct = default);
}

/// <summary>Outcome of a <see cref="IDefaultIepTemplateSeeder.SeedAsync"/> run.</summary>
public enum DefaultIepTemplateSeedOutcome
{
    /// <summary>The default IEP template + Published v1 was created by this run.</summary>
    Created = 0,
    /// <summary>A default IEP template already existed; nothing was written.</summary>
    AlreadySeeded = 1,
    /// <summary>Could not seed because the IEP document-type lookup row is missing (migrations not applied yet).</summary>
    SkippedNoDocumentType = 2
}

public sealed record DefaultIepTemplateSeedResult(DefaultIepTemplateSeedOutcome Outcome, int? DocumentTemplateVersionId = null);
