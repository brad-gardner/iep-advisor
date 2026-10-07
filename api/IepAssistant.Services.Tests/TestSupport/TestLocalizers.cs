using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace IepAssistant.Services.Tests.TestSupport;

/// <summary>
/// Builds real, resx-backed <see cref="IStringLocalizer{T}"/> instances for tests — deliberately NOT a
/// fake/stub, because several suites (AuthService/AuthController Spanish-message coverage,
/// EmailService Spanish-vs-English rendering) need to assert the actual translated text that ships, not
/// a placeholder. Mirrors the single <c>AddLocalization(o => o.ResourcesPath = "Resources")</c> call in
/// Api/Program.cs so resolution behaves identically to production.
/// </summary>
public static class TestLocalizers
{
    private static readonly IServiceProvider Provider = new ServiceCollection()
        .AddLogging() // ResourceManagerStringLocalizerFactory requires an ILoggerFactory.
        .AddLocalization(options => options.ResourcesPath = "Resources")
        .BuildServiceProvider();

    public static IStringLocalizer<Messages> Messages() => Provider.GetRequiredService<IStringLocalizer<Messages>>();

    public static IStringLocalizer<Emails> Emails() => Provider.GetRequiredService<IStringLocalizer<Emails>>();

    public static IStringLocalizer<Ai> Ai() => Provider.GetRequiredService<IStringLocalizer<Ai>>();

    public static IStringLocalizer<Notifications> Notifications() => Provider.GetRequiredService<IStringLocalizer<Notifications>>();

    public static IStringLocalizer<Pdf> Pdf() => Provider.GetRequiredService<IStringLocalizer<Pdf>>();
}
