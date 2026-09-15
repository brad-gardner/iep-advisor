using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// AI assist for template-driven document instances (IEP / ETR / 504). Targets are addressed by
/// field key and, for Table fields, the row's stable <c>_rowId</c>. Suggestions are returned, never
/// applied. Requires an active Collaborator+ grant on the document's student.
/// </summary>
public interface IDocumentAssistService
{
    Task<ServiceResult<AssistResultModel>> AssistAsync(int userId, int instanceId, Guid fieldKey, Guid? rowId, AssistKind kind, CancellationToken ct = default);
    Task<ServiceResult<ChatReplyModel>> ChatAsync(int userId, int instanceId, IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);
}
