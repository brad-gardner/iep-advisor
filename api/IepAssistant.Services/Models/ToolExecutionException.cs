namespace IepAssistant.Services.Models;

/// <summary>
/// The ONLY exception an <see cref="Interfaces.IToolExecutor"/> may throw to report a tool failure.
/// <c>ClaudeClient</c> turns it into a <c>tool_result</c> with <c>is_error = true</c> whose content is
/// <see cref="Exception.Message"/>, so the model can recover (retry with different input, tell the
/// user what it could not check). The message is sent to the model verbatim: keep it short, and never
/// include stack traces, ids belonging to other children, or raw exception text from dependencies.
/// Any other exception type propagates out of the stream untouched — catching those is the
/// executor's responsibility, not the client's.
/// </summary>
public sealed class ToolExecutionException : Exception
{
    public ToolExecutionException(string message) : base(message)
    {
    }
}
