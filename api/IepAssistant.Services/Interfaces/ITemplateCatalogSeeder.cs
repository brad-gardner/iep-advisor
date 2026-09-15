namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Seeds the launch-state template catalog (Ohio IEP PR-07, Ohio ETR PR-06, default Section 504) so every
/// document type resolves to a Published template. Idempotent per (StateCode, DocumentTypeId); runs at
/// startup after <see cref="IDefaultIepTemplateSeeder"/>.
/// </summary>
public interface ITemplateCatalogSeeder
{
    Task<TemplateCatalogSeedResult> SeedAsync(CancellationToken ct = default);
}

/// <summary>Names of templates created vs. already present/skipped by one run.</summary>
public sealed record TemplateCatalogSeedResult(IReadOnlyList<string> Created, IReadOnlyList<string> Skipped);
