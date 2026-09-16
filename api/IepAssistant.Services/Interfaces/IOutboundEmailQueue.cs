using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Persists a composed email for <c>OutboundEmailWorker</c> to actually send (pilot-gates
/// plan, phase 1, decision 2). Enqueueing is a plain INSERT — it does not throw on a delivery failure
/// (there is no delivery attempt yet); it throws only if the write itself fails (e.g. the database is
/// unreachable), which is the "no longer swallows" half of decision 2.</summary>
public interface IOutboundEmailQueue
{
    /// <summary>Returns the new <c>OutboundEmail</c> row's id.</summary>
    Task<int> EnqueueAsync(OutboundEmailDraft draft, CancellationToken ct = default);
}
