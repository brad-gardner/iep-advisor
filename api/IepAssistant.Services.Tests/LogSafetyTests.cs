using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

public class LogSafetyTests
{
    [Fact]
    public void Hash_NeverReturnsTheOriginalText()
    {
        var text = "This child's IEP mentions a diagnosis that must never appear in a log line.";
        var hash = LogSafety.Hash(text);

        Assert.DoesNotContain(text, hash);
        Assert.NotEqual(text, hash);
        Assert.Equal(12, hash.Length);
    }

    [Fact]
    public void Hash_IsDeterministic_ForTheSameInput()
    {
        Assert.Equal(LogSafety.Hash("same text"), LogSafety.Hash("same text"));
    }

    [Fact]
    public void Hash_DiffersForDifferentInput()
    {
        Assert.NotEqual(LogSafety.Hash("text a"), LogSafety.Hash("text b"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hash_ReturnsPlaceholder_ForNullOrEmpty(string? text)
    {
        Assert.Equal("(empty)", LogSafety.Hash(text));
    }
}
