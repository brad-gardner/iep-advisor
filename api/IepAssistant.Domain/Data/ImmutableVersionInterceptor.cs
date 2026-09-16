using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data;

/// <summary>
/// Enforces immutability of finalized <see cref="IepVersion"/> content (P5a) AND of
/// <b>Published</b> <see cref="DocumentTemplateVersion"/> template content (Phase 2). Any attempt to
/// <see cref="EntityState.Modified"/> or <see cref="EntityState.Deleted"/> a guarded entity throws.
/// <see cref="EntityState.Added"/> is allowed — finalize/fork inserts snapshots through this same
/// SaveChanges path.
///
/// <para><b>IepVersion family:</b> guarded unconditionally by type (a finalized version is always
/// immutable). <see cref="IepVersionPdf"/> is deliberately NOT guarded; the PDF render worker (P5b)
/// legitimately updates its RenderStatus/BlobUri/Checksum after rendering.</para>
///
/// <para><b>AuthoredDocumentVersion (Phase 4):</b> the dynamic-template equivalent — a finalized
/// <see cref="AuthoredDocumentVersion"/> (and its frozen ValuesJson) is guarded unconditionally by type.
/// Its <see cref="AuthoredDocumentPdf"/> is deliberately NOT guarded; the authored-document render worker
/// legitimately updates it after rendering (same split as IepVersion / IepVersionPdf).</para>
///
/// <para><b>SignatureStatus (plan 7, decision 4):</b> the ONE column on <see cref="AuthoredDocumentVersion"/>
/// itself that is allowed to change after finalize — a Modified entry is let through when every modified
/// property is <see cref="AuthoredDocumentVersion.SignatureStatus"/> or an audit stamp
/// (<c>UpdatedAt</c>/<c>UpdatedById</c>); any other modified property still throws.</para>
///
/// <para><b>Template family:</b> immutability is <em>state-dependent</em> — a Draft version and its
/// sections/fields must stay editable, only a <b>Published</b> version freezes. The publish transition
/// itself (Status Draft→Published) is allowed by checking the version's <em>original</em> status.
/// Sections/fields are frozen when their owning version is already Published (resolved via the
/// denormalized <c>DocumentTemplateVersionId</c>). The authoring service is the primary guard (every
/// mutation refuses a non-Draft version); this interceptor is defense-in-depth for direct-context
/// writes.</para>
///
/// <para><b>Limitation (documented, acceptable for now):</b> this interceptor sits in the
/// SaveChanges pipeline and therefore does NOT catch <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> or
/// raw SQL, which bypass the change tracker. Treat as a convention guard; if hard FERPA-grade
/// immutability is required later, add a DB trigger or revoke UPDATE/DELETE at the DB level. The
/// authoring service therefore never uses bulk ops on published rows.</para>
/// </summary>
public sealed class ImmutableVersionInterceptor : SaveChangesInterceptor
{
    private readonly ImmutabilityGuardBypass? _bypass;

    /// <summary>
    /// <paramref name="bypass"/> is optional so every existing call site that constructs this directly
    /// (<c>new ImmutableVersionInterceptor()</c> — production DI and every test fixture) keeps compiling
    /// and behaves exactly as before. Only the demo-reset scope (pilot-gates plan, phase 3) resolves this
    /// via DI and flips <see cref="ImmutabilityGuardBypass.Enabled"/> for the duration of its deletes.
    /// </summary>
    public ImmutableVersionInterceptor(ImmutabilityGuardBypass? bypass = null)
    {
        _bypass = bypass;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Guard(DbContext? context)
    {
        if (context is null || _bypass?.Enabled == true)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            if (IsImmutableVersionEntity(entry.Entity))
                throw new InvalidOperationException("IepVersion records are immutable.");

            if (entry.Entity is AuthoredDocumentVersion && !IsOnlySignatureStatusModified(entry))
                throw new InvalidOperationException("AuthoredDocumentVersion records are immutable.");

            if (IsFrozenTemplateEntity(context, entry))
                throw new InvalidOperationException("Published template versions are immutable.");
        }
    }

    private static bool IsImmutableVersionEntity(object entity) => entity is
        IepVersion or
        IepVersionSection or
        IepVersionGoal or
        IepVersionServiceLine or
        IepVersionAccommodation or
        IepVersionTransitionItem; // NOTE: IepVersionPdf intentionally absent.

    /// <summary>
    /// True when a modified/deleted template entity belongs to an already-Published version. For the
    /// version itself we read the <em>original</em> status so the Draft→Published publish transition is
    /// allowed; for sections/fields we read the owning version's current status (they are never touched
    /// during the publish transition).
    /// </summary>
    private static bool IsFrozenTemplateEntity(
        DbContext context, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        switch (entry.Entity)
        {
            case DocumentTemplateVersion:
                var status = entry.State == EntityState.Modified
                    ? (TemplateVersionStatus)entry.OriginalValues[nameof(DocumentTemplateVersion.Status)]!
                    : (TemplateVersionStatus)entry.CurrentValues[nameof(DocumentTemplateVersion.Status)]!;
                return status == TemplateVersionStatus.Published;

            case TemplateSection section:
                return VersionIsPublished(context, section.DocumentTemplateVersionId);

            case TemplateField field:
                return VersionIsPublished(context, field.DocumentTemplateVersionId);

            default:
                return false;
        }
    }

    /// <summary>True for a Modified <see cref="AuthoredDocumentVersion"/> entry when every modified
    /// property is <see cref="AuthoredDocumentVersion.SignatureStatus"/>/<c>UpdatedAt</c>/<c>UpdatedById</c>
    /// — i.e. a signature status update, never a content edit. Deleted entries are never exempted.</summary>
    private static bool IsOnlySignatureStatusModified(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State != EntityState.Modified)
            return false;

        return entry.Properties
            .Where(p => p.IsModified)
            .All(p => p.Metadata.Name is nameof(AuthoredDocumentVersion.SignatureStatus)
                or nameof(AuthoredDocumentVersion.UpdatedAt)
                or nameof(AuthoredDocumentVersion.UpdatedById));
    }

    private static bool VersionIsPublished(DbContext context, int versionId)
    {
        // Find checks the change tracker first, then the store (synchronous — acceptable in a guard).
        var version = context.Set<DocumentTemplateVersion>().Find(versionId);
        return version?.Status == TemplateVersionStatus.Published;
    }
}

/// <summary>
/// Scoped switch (pilot-gates plan, phase 3) that lets one specific, deliberate operation — the
/// demo-district reset (<c>DemoSeeder.ResetAsync</c>) — bypass <see cref="ImmutableVersionInterceptor"/>
/// for the lifetime of its own request/CLI scope. Defaults to disabled; nothing in normal request
/// handling ever sets it, so every other code path is guarded exactly as before. Registered Scoped
/// alongside the DbContext it is baked into (see <c>DependencyInjection.AddDomain</c>) — flipping it in
/// one scope cannot affect a concurrent scope's DbContext/interceptor instance.
/// </summary>
public sealed class ImmutabilityGuardBypass
{
    public bool Enabled { get; set; }
}
