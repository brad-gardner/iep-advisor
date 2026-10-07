namespace IepAssistant.Services;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;Notifications&gt;</c>, resolving to
/// <c>Resources/Notifications.resx</c> (English) and <c>Resources/Notifications.es.resx</c> (Spanish) via
/// the <c>ResourcesPath = "Resources"</c> configured in <c>AddLocalization</c> (Api/Program.cs) — see
/// <see cref="Messages"/> for why this lives in the project's root namespace rather than
/// <c>IepAssistant.Services.Resources</c>.
///
/// Multilingual plan (2026-10-06) phase 4: holds the title/body templates for every in-app/emailed
/// <c>Notification</c> row (<see cref="Services.Interfaces.INotificationService"/>). Deliberately a
/// SEPARATE resx from <c>Messages.resx</c> (ServiceResult/API failure text) and from <c>Emails.resx</c>
/// (owned by a different, concurrently-active phase-4 work item covering <c>EmailService</c>/
/// <c>IcsBuilder</c>) — this resx is never touched by that work.
///
/// <para>Every caller that creates a <c>Notification</c> builds its title/body through a
/// <c>Func&lt;string, (string Title, string Body)&gt;</c> passed to
/// <see cref="Services.Interfaces.INotificationService.NotifyAsync"/>, invoked once per distinct
/// recipient LANGUAGE (not once per recipient) inside a <see cref="Services.Localization.CultureScope"/>
/// already set to that language — so a plain <c>_localizer["Key"]</c> indexer call here resolves in the
/// right language without the callback needing to know anything about culture itself. A callback that
/// needs to render a DATE explicitly for that language (not the ambient, deliberately-untouched
/// <see cref="System.Globalization.CultureInfo.CurrentCulture"/>) uses
/// <see cref="Services.Localization.NotificationDateFormat"/> instead of an implicit
/// <c>DateTime.ToString()</c>.</para>
/// </summary>
public sealed class Notifications
{
}
