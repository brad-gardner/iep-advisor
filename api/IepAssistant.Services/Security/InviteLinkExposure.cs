namespace IepAssistant.Services.Security;

/// <summary>
/// Singleton gate for whether invite-creating responses may include the raw <c>inviteUrl</c> (e2e/testing
/// convenience). Resolved ONCE at startup (see <c>Program.cs</c>) from three conditions, ALL required:
/// <list type="number">
///   <item><c>Email:ExposeLinksForTesting == true</c> (default false)</item>
///   <item><c>Email:ConnectionString</c> is empty (no real ACS send configured)</item>
///   <item>the hosting environment is Development</item>
/// </list>
/// Startup is the only place <see cref="IHostEnvironment"/> is in scope, so enabling outside Development
/// is made impossible there (config flag is logged and ignored). Carrying the resolved boolean as a
/// singleton keeps the Services layer free of a hosting dependency and trivially testable.
/// </summary>
public sealed class InviteLinkExposure
{
    public bool Enabled { get; }

    public InviteLinkExposure(bool enabled) => Enabled = enabled;
}
