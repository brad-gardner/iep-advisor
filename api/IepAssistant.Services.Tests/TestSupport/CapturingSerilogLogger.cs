using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace IepAssistant.Services.Tests.TestSupport;

/// <summary>
/// Routes a service's <see cref="ILogger{T}"/> through a REAL Serilog pipeline (not a hand-rolled
/// fake) into an in-memory sink, so a log-content-safety test exercises the exact same
/// message-template rendering the API host uses in production (pilot-gates plan, phase 2). Backs
/// tests asserting that <c>DocumentAssistService</c>/<c>DraftQuestionService</c> never log a draft's
/// text or a parent's question — see <see cref="ContainsText"/>.
/// </summary>
public sealed class CapturingSerilogLogger : IDisposable
{
    /// <summary>Deliberately a SEPARATE object from <see cref="CapturingSerilogLogger"/> itself — if
    /// this class implemented <see cref="ILogEventSink"/> directly and registered itself as the sink,
    /// Serilog's own <c>Logger.Dispose()</c> would dispose any of its sinks that implement
    /// <see cref="IDisposable"/>, which (since this class also implements IDisposable) would call
    /// right back into this class's own <see cref="Dispose"/> — infinite recursion, observed as a
    /// stack overflow that crashed the test host.</summary>
    private sealed class Sink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private readonly Sink _sink = new();
    private readonly Serilog.ILogger _serilogLogger;
    private readonly LoggerFactory _loggerFactory;

    public CapturingSerilogLogger()
    {
        _serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(_sink)
            .CreateLogger();

        _loggerFactory = new LoggerFactory();
        _loggerFactory.AddSerilog(_serilogLogger);
    }

    public ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>True if <paramref name="text"/> (case-insensitive) appears anywhere in any captured
    /// event: the rendered message, any structured property's value, or an attached exception's
    /// message/ToString — not just the literal message template.</summary>
    public bool ContainsText(string text)
    {
        foreach (var logEvent in _sink.Events)
        {
            if (logEvent.RenderMessage().Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var property in logEvent.Properties.Values)
            {
                if (property.ToString().Contains(text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (logEvent.Exception != null && logEvent.Exception.ToString().Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public int EventCount => _sink.Events.Count;

    public void Dispose()
    {
        _loggerFactory.Dispose();
        (_serilogLogger as IDisposable)?.Dispose();
    }
}
