using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// The Virtual Advocate: a parent's private, tool-grounded chat about one child. Threads belong to the
/// parent who started them (<c>AdvocateThread.ParentUserId</c>) and are never visible to co-parents;
/// creating a thread and sending a message require Collaborator+ on the child.
/// </summary>
public interface IAdvocateService
{
    Task<ServiceResult<List<AdvocateThreadModel>>> ListThreadsAsync(int userId, int childId, CancellationToken ct = default);
    Task<ServiceResult<AdvocateThreadModel>> CreateThreadAsync(int userId, int childId, string? title, CancellationToken ct = default);
    Task<ServiceResult<AdvocateThreadDetailModel>> GetThreadAsync(int userId, int threadId, CancellationToken ct = default);
    Task<ServiceResult> RenameThreadAsync(int userId, int threadId, string title, CancellationToken ct = default);
    Task<ServiceResult> DeleteThreadAsync(int userId, int threadId, CancellationToken ct = default);
    Task<ServiceResult<AdvocateUsageModel>> GetUsageAsync(int userId, CancellationToken ct = default);
    /// <summary>Viewer+: the child's resolved state so the UI can tell the parent whether state rules apply.</summary>
    Task<ServiceResult<AdvocateChildContextModel>> GetChildContextAsync(int userId, int childId, CancellationToken ct = default);

    /// <summary>
    /// Validates, persists the user message, streams the answer, persists the assistant message. Failures
    /// arrive as an <see cref="AdvocateStreamEventKind.Error"/> event; the enumerable never throws
    /// <see cref="ClaudeApiException"/>. Cancelling <paramref name="ct"/> aborts the turn (no assistant row).
    /// </summary>
    IAsyncEnumerable<AdvocateStreamEvent> SendMessageAsync(int userId, int threadId, string text, string? about, CancellationToken ct = default);
}
