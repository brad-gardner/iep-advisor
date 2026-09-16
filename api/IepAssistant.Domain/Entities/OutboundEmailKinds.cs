namespace IepAssistant.Domain.Entities;

/// <summary>
/// The <see cref="OutboundEmail.Kind"/> values whose body carries a one-time secret (a sign-in link,
/// a reset or invite token, a cancel-deletion link). Stored bodies for these kinds are redacted once
/// the row is terminal, and they cannot be re-sent from the queue — a fresh link must be requested.
/// </summary>
public static class OutboundEmailKinds
{
    public const string RedactedBody = "[redacted — this message carried a one-time link]";

    private static readonly HashSet<string> OneTimeSecretKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "MagicLink", "PasswordReset", "AccountDeletionCancelLink", "ShareInvite", "SchoolLinkInvite",
        "StudentInvite", "StaffInvite", "BetaInvite",
        // Meeting notifications carry the participant's RSVP token (a bearer credential until the meeting starts).
        "MeetingInvitation", "MeetingUpdated", "MeetingCancelled",
    };

    public static bool CarriesOneTimeSecret(string? kind) => kind != null && OneTimeSecretKinds.Contains(kind);

    public static bool IsRedacted(OutboundEmail email) => email.HtmlBody == RedactedBody;
}
