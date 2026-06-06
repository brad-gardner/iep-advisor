using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Educator AI assist (P6b) behind Feature:SchoolSide. Inline single-field assists and an
/// ephemeral IEP-scoped chat, both backed by <see cref="IClaudeClient"/>. Every entry point
/// requires Collaborator+ SchoolStudentAccess on the draft's student (SchoolId-bound), mirroring
/// <c>IepDraftService</c>/<c>IepVersionService</c>. Assists return suggestion text only — they
/// never mutate the draft (the educator accepts/applies on the client). Chat is stateless: the
/// client resends the whole thread each call and nothing is persisted.
/// </summary>
public interface IIepAssistService
{
    Task<ServiceResult<AssistResultModel>> AssistGoalAsync(int userId, int draftId, int goalId, AssistKind kind, CancellationToken ct = default);
    Task<ServiceResult<AssistResultModel>> AssistSectionAsync(int userId, int draftId, int sectionId, AssistKind kind, CancellationToken ct = default);
    Task<ServiceResult<AssistResultModel>> AssistServiceLineAsync(int userId, int draftId, int serviceLineId, AssistKind kind, CancellationToken ct = default);
    Task<ServiceResult<ChatReplyModel>> ChatAsync(int userId, int draftId, IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);
}
