namespace IepAssistant.Services;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;Messages&gt;</c>, resolving to <c>Resources/Messages.resx</c>
/// (English) and <c>Resources/Messages.es.resx</c> (Spanish) via the <c>ResourcesPath = "Resources"</c>
/// configured in <c>AddLocalization</c> (Api/Program.cs). Deliberately declared in the project's root
/// namespace (<c>IepAssistant.Services</c>, not <c>IepAssistant.Services.Resources</c>) — the default
/// resource-name convention is RootNamespace + ResourcesPath + type name, and putting the marker in a
/// nested namespace would make the localizer look for the resx one folder deeper than it actually lives.
/// Holds server-message strings (<c>ServiceResult</c>/<c>ApiResponse</c> text) for AuthService/
/// AuthController (plan 2026-10-06 phase 1); later phases add more services' messages to the same file.
/// </summary>
public sealed class Messages
{
}
