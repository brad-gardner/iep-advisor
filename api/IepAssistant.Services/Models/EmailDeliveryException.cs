namespace IepAssistant.Services.Models;

/// <summary>
/// Thrown by <see cref="Interfaces.IEmailTransport.SendAsync"/> when an actual send attempt fails
/// (pilot-gates plan, phase 1, decision 2). Caught only by <c>OutboundEmailWorker</c>, which records
/// <see cref="Message"/>/<see cref="Exception.InnerException"/> on the <c>OutboundEmail</c> row —
/// nothing upstream of the transport ever sees this swallowed.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public string ToEmail { get; }
    public string Subject { get; }

    public EmailDeliveryException(string toEmail, string subject, Exception inner)
        : base("Failed to deliver an outbound email.", inner)
    {
        ToEmail = toEmail;
        Subject = subject;
    }
}
