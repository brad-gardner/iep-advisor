using System.Text.Json;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>See <see cref="IOutboundEmailQueue"/>.</summary>
public class OutboundEmailQueue : IOutboundEmailQueue
{
    /// <summary>Internal shape of one attachment inside <c>OutboundEmail.AttachmentsJson</c>. Not a
    /// public model — the wire shape between enqueue and the worker's decode, nothing else reads it.</summary>
    private sealed record AttachmentJson(string Name, string ContentType, string Base64);

    private readonly ApplicationDbContext _context;

    public OutboundEmailQueue(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> EnqueueAsync(OutboundEmailDraft draft, CancellationToken ct = default)
    {
        string? attachmentsJson = null;
        if (draft.Attachments is { Count: > 0 })
        {
            var attachments = draft.Attachments
                .Select(a => new AttachmentJson(a.Name, a.ContentType, Convert.ToBase64String(a.Content)))
                .ToList();
            attachmentsJson = JsonSerializer.Serialize(attachments);
        }

        var row = new OutboundEmail
        {
            ToEmail = draft.ToEmail,
            Subject = draft.Subject,
            HtmlBody = draft.HtmlBody,
            TextBody = draft.TextBody,
            Kind = draft.Kind,
            Status = OutboundEmailStatus.Queued,
            NextAttemptAt = DateTime.UtcNow,
            CorrelationId = draft.CorrelationId,
            AttachmentsJson = attachmentsJson
        };

        _context.OutboundEmails.Add(row);
        await _context.SaveChangesAsync(ct);
        return row.Id;
    }

    /// <summary>Decodes <c>AttachmentsJson</c> back into transport-ready attachments. Public (rather
    /// than a shared model) so <c>OutboundEmailWorker</c> in the Api project can use the exact same wire
    /// shape as <see cref="EnqueueAsync"/> without either project needing a public DTO for it.</summary>
    public static List<OutboundEmailAttachmentDraft>? DecodeAttachments(string? attachmentsJson)
    {
        if (string.IsNullOrEmpty(attachmentsJson))
            return null;

        var decoded = JsonSerializer.Deserialize<List<AttachmentJson>>(attachmentsJson);
        return decoded?.Select(a => new OutboundEmailAttachmentDraft(a.Name, a.ContentType, Convert.FromBase64String(a.Base64))).ToList();
    }
}
