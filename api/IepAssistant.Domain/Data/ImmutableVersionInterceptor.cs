using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data;

/// <summary>
/// Enforces immutability of finalized <see cref="IepVersion"/> content (P5a). Any attempt to
/// <see cref="EntityState.Modified"/> or <see cref="EntityState.Deleted"/> an IepVersion or one of
/// its content children throws. <see cref="EntityState.Added"/> is allowed — finalize inserts the
/// snapshot through this same SaveChanges path.
///
/// <para><b>Excluded:</b> <see cref="IepVersionPdf"/> is deliberately NOT guarded; the PDF render
/// worker (P5b) legitimately updates its RenderStatus/BlobUri/Checksum after rendering.</para>
///
/// <para><b>Limitation (documented, acceptable for now):</b> this interceptor sits in the
/// SaveChanges pipeline and therefore does NOT catch <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> or
/// raw SQL, which bypass the change tracker. Treat as a convention guard; if hard FERPA-grade
/// immutability is required later, add a DB trigger or revoke UPDATE/DELETE at the DB level.</para>
/// </summary>
public sealed class ImmutableVersionInterceptor : SaveChangesInterceptor
{
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

    private static void Guard(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            if (IsImmutableVersionEntity(entry.Entity))
                throw new InvalidOperationException("IepVersion records are immutable.");
        }
    }

    private static bool IsImmutableVersionEntity(object entity) => entity is
        IepVersion or
        IepVersionSection or
        IepVersionGoal or
        IepVersionServiceLine or
        IepVersionAccommodation or
        IepVersionTransitionItem; // NOTE: IepVersionPdf intentionally absent.
}
