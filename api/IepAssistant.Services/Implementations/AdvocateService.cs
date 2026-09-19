using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Data.Configurations;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// See <see cref="IAdvocateService"/>. Threads are private to the asking parent (owner check is
/// <c>ParentUserId == userId</c>, never "has access to the child"); the child-level role gate is Viewer+ to
/// read and Collaborator+ to create/send. Every failure that can be decided before the model is called is
/// the first event of <see cref="SendMessageAsync"/>, so the transport can answer with a status code.
/// </summary>
public class AdvocateService : IAdvocateService
{
    public const string DefaultTitle = "New conversation";
    public const string OperationType = "advocate_message";
    public const int MaxTextLength = AdvocateMessageConfiguration.UserContentMaxLength;
    public const int MaxTitleLength = AdvocateThreadConfiguration.TitleMaxLength;
    public const int FreeMessageCap = 20;
    public const int ActiveSubscriptionMessageCap = 300;
    public const int HistoryMessageCount = 12;
    public const int HistoryCharBudget = 20_000;
    public const int MaxTokens = 8192;
    public static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(120);

    private const string ChildNotFound = "Child profile not found.";
    private const string ThreadNotFound = "Conversation not found.";
    private const string CollaboratorRequired = "You can view this child but cannot ask the advocate about them.";

    private static readonly Regex AboutGrammar = new(@"^(iep|etr|goal|analysis|progress_report|journal):(\d{1,9})$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _access;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IIepComparisonService _comparison;
    private readonly IClaudeClient _claude;
    private readonly ILogger<AdvocateService> _logger;

    public AdvocateService(ApplicationDbContext context, IAccessService access, IKnowledgeBaseService knowledgeBase, IIepComparisonService comparison, IClaudeClient claude, ILogger<AdvocateService> logger)
    {
        _context = context;
        _access = access;
        _knowledgeBase = knowledgeBase;
        _comparison = comparison;
        _claude = claude;
        _logger = logger;
    }

    // ------------------------------------------------------------------ threads

    public async Task<ServiceResult<List<AdvocateThreadModel>>> ListThreadsAsync(int userId, int childId, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
            return ServiceResult<List<AdvocateThreadModel>>.FailureResult(ChildNotFound);

        var threads = await _context.AdvocateThreads.AsNoTracking()
            .Where(t => t.ChildProfileId == childId && t.ParentUserId == userId)
            .OrderByDescending(t => t.LastMessageAt).ThenByDescending(t => t.Id)
            .ToListAsync(ct);
        return ServiceResult<List<AdvocateThreadModel>>.SuccessResult(threads.Select(MapThread).ToList());
    }

    public async Task<ServiceResult<AdvocateThreadModel>> CreateThreadAsync(int userId, int childId, string? title, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<AdvocateThreadModel>.FailureResult(ChildNotFound);

        var (cleanTitle, error) = NormalizeTitle(title, allowEmpty: true);
        if (error != null) return ServiceResult<AdvocateThreadModel>.FailureResult(error);

        var now = DateTime.UtcNow;
        var thread = new AdvocateThread
        {
            ChildProfileId = childId,
            ParentUserId = userId,
            Title = cleanTitle ?? DefaultTitle,
            CreatedAt = now,
            UpdatedAt = now,
            LastMessageAt = now
        };
        _context.AdvocateThreads.Add(thread);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<AdvocateThreadModel>.SuccessResult(MapThread(thread));
    }

    public async Task<ServiceResult<AdvocateThreadDetailModel>> GetThreadAsync(int userId, int threadId, CancellationToken ct = default)
    {
        var thread = await LoadOwnedThreadAsync(userId, threadId, AccessRole.Viewer, track: false, ct);
        if (thread == null)
            return ServiceResult<AdvocateThreadDetailModel>.FailureResult(ThreadNotFound);

        var messages = await _context.AdvocateMessages.AsNoTracking()
            .Where(m => m.AdvocateThreadId == threadId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync(ct);

        var detail = new AdvocateThreadDetailModel
        {
            Id = thread.Id,
            ChildProfileId = thread.ChildProfileId,
            Title = thread.Title,
            CreatedAt = thread.CreatedAt,
            UpdatedAt = thread.UpdatedAt,
            LastMessageAt = thread.LastMessageAt,
            Messages = messages.Select(MapMessage).ToList()
        };
        return ServiceResult<AdvocateThreadDetailModel>.SuccessResult(detail);
    }

    public async Task<ServiceResult> RenameThreadAsync(int userId, int threadId, string title, CancellationToken ct = default)
    {
        var (cleanTitle, error) = NormalizeTitle(title, allowEmpty: false);
        if (error != null) return ServiceResult.FailureResult(error);

        var thread = await LoadOwnedThreadAsync(userId, threadId, AccessRole.Viewer, track: true, ct);
        if (thread == null) return ServiceResult.FailureResult(ThreadNotFound);

        thread.Title = cleanTitle!;
        thread.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult> DeleteThreadAsync(int userId, int threadId, CancellationToken ct = default)
    {
        var thread = await LoadOwnedThreadAsync(userId, threadId, AccessRole.Viewer, track: true, ct);
        if (thread == null) return ServiceResult.FailureResult(ThreadNotFound);

        // Explicit so the delete never leans on the provider honouring the cascade.
        _context.AdvocateMessages.RemoveRange(_context.AdvocateMessages.Where(m => m.AdvocateThreadId == threadId));
        _context.AdvocateThreads.Remove(thread);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    // ------------------------------------------------------------------ usage

    public async Task<ServiceResult<AdvocateUsageModel>> GetUsageAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.SubscriptionStatus, u.SubscriptionExpiresAt })
            .FirstOrDefaultAsync(ct);
        if (user == null) return ServiceResult<AdvocateUsageModel>.FailureResult("User not found.");

        var usage = await CountUsageAsync(userId, user.SubscriptionStatus, user.SubscriptionExpiresAt, ct);
        return ServiceResult<AdvocateUsageModel>.SuccessResult(usage);
    }

    private async Task<AdvocateUsageModel> CountUsageAsync(int userId, string subscriptionStatus, DateTime? subscriptionExpiresAt, CancellationToken ct)
    {
        var active = string.Equals(subscriptionStatus, "active", StringComparison.OrdinalIgnoreCase);
        var since = GetSubscriptionYearStart(subscriptionExpiresAt);
        var used = await _context.UsageRecords.CountAsync(
            u => u.UserId == userId && u.OperationType == OperationType && u.CreatedAt >= since, ct);
        return new AdvocateUsageModel { Used = used, Limit = active ? ActiveSubscriptionMessageCap : FreeMessageCap, SubscriptionActive = active };
    }

    /// <summary>Same rule as <c>SubscriptionService.GetSubscriptionYearStart</c>: one year before expiry, or a trailing year when there is no expiry.</summary>
    public static DateTime GetSubscriptionYearStart(DateTime? subscriptionExpiresAt) =>
        subscriptionExpiresAt == null ? DateTime.UtcNow.AddYears(-1) : subscriptionExpiresAt.Value.AddYears(-1);

    // ------------------------------------------------------------------ send

    public async IAsyncEnumerable<AdvocateStreamEvent> SendMessageAsync(int userId, int threadId, string text, string? about, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (turn, preCheckError) = await PrepareTurnAsync(userId, threadId, text, about, ct);
        if (preCheckError != null)
        {
            yield return preCheckError;
            yield break;
        }

        // C# iterators cannot yield inside a try/catch, so the model call runs as a producer that writes
        // into a channel and converts every failure into an Error event; this loop only forwards.
        var channel = Channel.CreateUnbounded<AdvocateStreamEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TurnTimeout);
        var producer = ProduceAsync(turn!, channel.Writer, ct, linked.Token);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
                yield return evt;
        }
        finally
        {
            // A consumer that stops early (client went away) must not leave the producer running against the
            // request-scoped DbContext after this scope is gone.
            if (!producer.IsCompleted) linked.Cancel();
            try { await producer; } catch (OperationCanceledException) { }
        }
    }

    private sealed record PreparedTurn(AdvocateThread Thread, ClaudeToolRequest Request, AdvocateToolset Toolset);

    private async Task<(PreparedTurn? Turn, AdvocateStreamEvent? Error)> PrepareTurnAsync(int userId, int threadId, string text, string? about, CancellationToken ct)
    {
        var question = (text ?? string.Empty).Trim();
        if (question.Length == 0)
            return (null, AdvocateStreamEvent.Error(AdvocateErrorCodes.Validation, "Message is required."));
        if (question.Length > MaxTextLength)
            return (null, AdvocateStreamEvent.Error(AdvocateErrorCodes.Validation, $"Message must be {MaxTextLength} characters or fewer."));

        var aboutSentence = RenderAbout(about);
        if (about != null && aboutSentence == null)
            return (null, AdvocateStreamEvent.Error(AdvocateErrorCodes.Validation, "about is not a recognised record reference."));

        var thread = await _context.AdvocateThreads.FirstOrDefaultAsync(t => t.Id == threadId, ct);
        if (thread == null || thread.ParentUserId != userId)
            return (null, AdvocateStreamEvent.Error(AdvocateErrorCodes.NotFound, ThreadNotFound));
        if (!await _access.HasMinimumRoleAsync(thread.ChildProfileId, userId, AccessRole.Collaborator, ct))
        {
            return (null, await _access.HasMinimumRoleAsync(thread.ChildProfileId, userId, AccessRole.Viewer, ct)
                ? AdvocateStreamEvent.Error(AdvocateErrorCodes.Forbidden, CollaboratorRequired)
                : AdvocateStreamEvent.Error(AdvocateErrorCodes.NotFound, ThreadNotFound));
        }

        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.SubscriptionStatus, u.SubscriptionExpiresAt, u.State })
            .FirstOrDefaultAsync(ct);
        if (user == null)
            return (null, AdvocateStreamEvent.Error(AdvocateErrorCodes.NotFound, ThreadNotFound));

        var usage = await CountUsageAsync(userId, user.SubscriptionStatus, user.SubscriptionExpiresAt, ct);
        if (usage.Used >= usage.Limit)
            return (null, AdvocateStreamEvent.Error(AdvocateErrorCodes.UsageCap, AdvocatePrompts.UsageCapMessage));

        var child = await _context.ChildProfiles.AsNoTracking()
            .Where(c => c.Id == thread.ChildProfileId)
            .Select(c => new { c.FirstName, c.GradeLevel })
            .FirstOrDefaultAsync(ct);
        if (child == null)
            return (null, AdvocateStreamEvent.Error(AdvocateErrorCodes.NotFound, ThreadNotFound));

        var stateCode = await ChildStateResolver.ResolveAsync(_context, thread.ChildProfileId, ct);

        // History is everything BEFORE this message: read it before the user row is added.
        var history = await BuildHistoryAsync(threadId, ct);

        var now = DateTime.UtcNow;
        _context.AdvocateMessages.Add(new AdvocateMessage
        {
            AdvocateThreadId = threadId,
            Role = AdvocateMessageRole.User,
            ContentMarkdown = question,
            CreatedAt = now
        });
        thread.LastMessageAt = now;
        thread.UpdatedAt = now;
        await _context.SaveChangesAsync(ct);

        var context = new StringBuilder();
        context.AppendLine("<context>");
        context.AppendLine($"Child's first name: {PromptText.Data(PromptText.OneLine(child.FirstName))}");
        context.AppendLine($"Grade: {(string.IsNullOrWhiteSpace(child.GradeLevel) ? "unknown" : PromptText.Data(PromptText.OneLine(child.GradeLevel)))}");
        context.AppendLine($"State: {stateCode ?? "unknown"}");
        context.AppendLine($"Today's date (UTC): {now:yyyy-MM-dd}");
        if (aboutSentence != null) context.AppendLine(aboutSentence);
        context.AppendLine("</context>");
        context.Append($"<question>{PromptText.Data(question)}</question>");
        history.Add(new ClaudeTurn("user", context.ToString()));

        var toolset = new AdvocateToolset(_context, _access, _knowledgeBase, _comparison, thread.ChildProfileId, userId, stateCode, _logger);
        var request = new ClaudeToolRequest
        {
            SystemPrompt = AdvocatePrompts.System,
            Messages = history,
            Tools = toolset.Definitions,
            MaxTokens = MaxTokens
        };
        return (new PreparedTurn(thread, request, toolset), null);
    }

    private async Task ProduceAsync(PreparedTurn turn, ChannelWriter<AdvocateStreamEvent> writer, CancellationToken callerCt, CancellationToken turnCt)
    {
        try
        {
            ClaudeStreamEvent? completed = null;
            await foreach (var evt in _claude.StreamWithToolsAsync(turn.Request, turn.Toolset, turnCt))
            {
                switch (evt.Kind)
                {
                    case ClaudeStreamEventKind.TextDelta:
                        if (!string.IsNullOrEmpty(evt.Text))
                            await writer.WriteAsync(AdvocateStreamEvent.Delta(evt.Text), turnCt);
                        break;
                    case ClaudeStreamEventKind.ToolStarted:
                        await writer.WriteAsync(AdvocateStreamEvent.Tool(evt.ToolName ?? string.Empty, AdvocatePrompts.ToolLabel(evt.ToolName ?? string.Empty, evt.ToolInput), "started"), turnCt);
                        break;
                    case ClaudeStreamEventKind.ToolFinished:
                        await writer.WriteAsync(AdvocateStreamEvent.Tool(evt.ToolName ?? string.Empty, AdvocatePrompts.ToolLabel(evt.ToolName ?? string.Empty, evt.ToolInput), evt.ToolIsError ? "failed" : "finished"), turnCt);
                        break;
                    case ClaudeStreamEventKind.Completed:
                        completed = evt;
                        break;
                }
            }

            if (completed == null || string.IsNullOrWhiteSpace(completed.FullText))
            {
                _logger.LogWarning("Advocate turn on thread {ThreadId}: Claude returned no content.", turn.Thread.Id);
                await writer.WriteAsync(AdvocateStreamEvent.Error(AdvocateErrorCodes.Unavailable, AdvocatePrompts.UnavailableMessage), turnCt);
                return;
            }

            var done = await PersistAnswerAsync(turn, completed, turnCt);
            await writer.WriteAsync(done, turnCt);
        }
        catch (ClaudeApiException ex)
        {
            _logger.LogError(ex, "Advocate turn on thread {ThreadId} failed with {Kind}", turn.Thread.Id, ex.Kind);
            await TryWriteErrorAsync(writer, AdvocatePrompts.UnavailableMessage);
        }
        catch (OperationCanceledException) when (!callerCt.IsCancellationRequested)
        {
            _logger.LogWarning("Advocate turn on thread {ThreadId} timed out after {Timeout}s", turn.Thread.Id, TurnTimeout.TotalSeconds);
            await TryWriteErrorAsync(writer, ClaudeFailureMessages.Timeout);
        }
        catch (OperationCanceledException)
        {
            // Caller went away: nothing to report to.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advocate turn on thread {ThreadId} failed unexpectedly", turn.Thread.Id);
            await TryWriteErrorAsync(writer, AdvocatePrompts.UnavailableMessage);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static async Task TryWriteErrorAsync(ChannelWriter<AdvocateStreamEvent> writer, string message)
    {
        try { await writer.WriteAsync(AdvocateStreamEvent.Error(AdvocateErrorCodes.Unavailable, message)); }
        catch (ChannelClosedException) { }
    }

    private async Task<AdvocateStreamEvent> PersistAnswerAsync(PreparedTurn turn, ClaudeStreamEvent completed, CancellationToken ct)
    {
        var parsed = AdvocateAnswerParser.Parse(completed.FullText, turn.Toolset.ReturnedRefs, turn.Toolset.Labels, turn.Toolset.Parents);
        var markdown = parsed.Markdown.Length > AdvocateMessageConfiguration.AssistantContentMaxLength
            ? parsed.Markdown[..AdvocateMessageConfiguration.AssistantContentMaxLength]
            : parsed.Markdown;
        var truncated = completed.Truncated || markdown.Length < parsed.Markdown.Length;

        var now = DateTime.UtcNow;
        var message = new AdvocateMessage
        {
            AdvocateThreadId = turn.Thread.Id,
            Role = AdvocateMessageRole.Assistant,
            ContentMarkdown = markdown,
            CitationsJson = parsed.Citations.Count == 0 ? null : JsonSerializer.Serialize(parsed.Citations, Json),
            SuggestionsJson = parsed.Suggestions.Count == 0 ? null : JsonSerializer.Serialize(parsed.Suggestions, Json),
            ToolTraceJson = completed.Trace == null ? null : JsonSerializer.Serialize(completed.Trace, Json),
            InputTokens = completed.InputTokens,
            OutputTokens = completed.OutputTokens,
            Truncated = truncated,
            CreatedAt = now
        };
        _context.AdvocateMessages.Add(message);
        _context.UsageRecords.Add(new UsageRecord
        {
            UserId = turn.Thread.ParentUserId,
            ChildProfileId = turn.Thread.ChildProfileId,
            DistrictId = null,
            OperationType = OperationType,
            CreatedAt = now
        });
        turn.Thread.LastMessageAt = now;
        turn.Thread.UpdatedAt = now;
        await _context.SaveChangesAsync(ct);

        return AdvocateStreamEvent.Done(message.Id, markdown, parsed.Citations, parsed.Suggestions, truncated, AdvocatePrompts.Disclaimer);
    }

    /// <summary>
    /// The last <see cref="HistoryMessageCount"/> messages oldest-first, trimmed to <see cref="HistoryCharBudget"/>
    /// by dropping the oldest first, then trimmed again so the conversation starts with a user turn. User text
    /// is re-wrapped as data (it is parent-typed); assistant text is our own stored markdown.
    /// </summary>
    private async Task<List<ClaudeTurn>> BuildHistoryAsync(int threadId, CancellationToken ct)
    {
        var recent = await _context.AdvocateMessages.AsNoTracking()
            .Where(m => m.AdvocateThreadId == threadId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(HistoryMessageCount)
            .Select(m => new { m.Role, m.ContentMarkdown })
            .ToListAsync(ct);

        var kept = new List<ClaudeTurn>();
        var used = 0;
        foreach (var m in recent) // newest first
        {
            var text = m.Role == AdvocateMessageRole.User ? $"<question>{PromptText.Data(m.ContentMarkdown)}</question>" : m.ContentMarkdown;
            if (text.Length == 0) continue;
            if (used + text.Length > HistoryCharBudget) break;
            used += text.Length;
            kept.Add(new ClaudeTurn(m.Role == AdvocateMessageRole.User ? "user" : "assistant", text));
        }
        kept.Reverse();
        while (kept.Count > 0 && kept[0].Role != "user")
            kept.RemoveAt(0);
        return kept;
    }

    /// <summary>
    /// The launcher context, rendered by US: the raw client value never reaches the prompt. Null when
    /// <paramref name="about"/> is null OR outside the grammar — the caller distinguishes the two.
    /// </summary>
    internal static string? RenderAbout(string? about)
    {
        if (about == null) return null;
        var match = AboutGrammar.Match(about.Trim());
        if (!match.Success) return null;
        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return null;

        var noun = match.Groups[1].Value switch
        {
            "iep" => "IEP document",
            "etr" => "ETR document",
            "goal" => "IEP goal",
            "analysis" => "analysis",
            "progress_report" => "progress report",
            "journal" => "journal entry",
            _ => null
        };
        return noun == null ? null : $"The parent opened this conversation from their {noun} #{id}.";
    }

    // ------------------------------------------------------------------ helpers

    private async Task<AdvocateThread?> LoadOwnedThreadAsync(int userId, int threadId, AccessRole minRole, bool track, CancellationToken ct)
    {
        var query = track ? _context.AdvocateThreads : _context.AdvocateThreads.AsNoTracking();
        var thread = await query.FirstOrDefaultAsync(t => t.Id == threadId, ct);
        if (thread == null || thread.ParentUserId != userId) return null;
        // Access revoked since the thread was created ⇒ the same "not found", never a reveal.
        if (!await _access.HasMinimumRoleAsync(thread.ChildProfileId, userId, minRole, ct)) return null;
        return thread;
    }

    private static (string? Title, string? Error) NormalizeTitle(string? title, bool allowEmpty)
    {
        var clean = (title ?? string.Empty).Trim();
        if (clean.Length == 0) return allowEmpty ? (null, null) : (null, "Title is required.");
        if (clean.Length > MaxTitleLength) return (null, $"Title must be {MaxTitleLength} characters or fewer.");
        return (PromptText.OneLine(clean), null);
    }

    private static AdvocateThreadModel MapThread(AdvocateThread t) => new()
    {
        Id = t.Id,
        ChildProfileId = t.ChildProfileId,
        Title = t.Title,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        LastMessageAt = t.LastMessageAt
    };

    private static AdvocateMessageModel MapMessage(AdvocateMessage m) => new()
    {
        Id = m.Id,
        Role = m.Role,
        ContentMarkdown = m.ContentMarkdown,
        Citations = ParseJson<AdvocateCitation>(m.CitationsJson),
        Suggestions = ParseJson<AdvocateSuggestion>(m.SuggestionsJson),
        Truncated = m.Truncated,
        CreatedAt = m.CreatedAt
    };

    private static List<T> ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try { return JsonSerializer.Deserialize<List<T>>(json, Json) ?? new List<T>(); }
        catch (JsonException) { return new List<T>(); }
    }
}
