using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IClaudeClient
{
    Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default);
}
