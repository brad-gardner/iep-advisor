using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Pilot-gates plan, phase 1, decision 2: outside Development, an unconfigured transport must be a
/// visible delivery FAILURE (a Failed row an admin can see and retry once ACS is configured) rather
/// than a silent "success" — this is the mechanism behind the API host's startup WARN banner.
/// </summary>
public class AcsEmailTransportTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static AcsEmailTransport BuildTransport(string environmentName, string? connectionString) =>
        new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:ConnectionString"] = connectionString
            }).Build(),
            new FakeHostEnvironment { EnvironmentName = environmentName },
            NullLogger<AcsEmailTransport>.Instance);

    [Fact]
    public void IsConfigured_DevelopmentWithNoConnectionString_IsTrue()
    {
        var transport = BuildTransport(Environments.Development, connectionString: null);
        Assert.True(transport.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ProductionWithNoConnectionString_IsFalse()
    {
        var transport = BuildTransport(Environments.Production, connectionString: null);
        Assert.False(transport.IsConfigured);
    }

    [Fact]
    public void IsConfigured_AnyEnvironmentWithConnectionString_IsTrue()
    {
        var transport = BuildTransport(Environments.Production, connectionString: "endpoint=https://fake;accesskey=fake");
        Assert.True(transport.IsConfigured);
    }

    [Fact]
    public async Task SendAsync_DevelopmentWithNoConnectionString_Succeeds()
    {
        var transport = BuildTransport(Environments.Development, connectionString: null);
        await transport.SendAsync("parent@example.com", "Subject", "<p>hi</p>", "hi", null);
        // No exception — this IS success (matches the product's pre-existing local/CI behavior).
    }

    [Fact]
    public async Task SendAsync_ProductionWithNoConnectionString_ThrowsEmailDeliveryException()
    {
        var transport = BuildTransport(Environments.Production, connectionString: null);

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => transport.SendAsync("parent@example.com", "Subject", "<p>hi</p>", "hi", null));

        Assert.Equal("parent@example.com", ex.ToEmail);
        Assert.Equal("Subject", ex.Subject);
    }

    [Fact]
    public async Task SendAsync_StagingWithNoConnectionString_ThrowsEmailDeliveryException()
    {
        // Any non-Development environment, not just literally "Production".
        var transport = BuildTransport("Staging", connectionString: null);

        await Assert.ThrowsAsync<EmailDeliveryException>(
            () => transport.SendAsync("parent@example.com", "Subject", "<p>hi</p>", "hi", null));
    }
}
