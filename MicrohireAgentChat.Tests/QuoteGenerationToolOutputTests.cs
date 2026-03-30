using System.Text.Json;
using MicrohireAgentChat.Services;

namespace MicrohireAgentChat.Tests;

/// <summary>
/// Contract tests for generate_quote tool JSON (text confirmation + View quote / Accept UI).
/// </summary>
public sealed class QuoteGenerationToolOutputTests
{
    [Fact]
    public void SerializeAwaitingExplicitUserConfirmation_ContainsLockedMessageAndInstruction()
    {
        var json = QuoteGenerationToolOutput.SerializeAwaitingExplicitUserConfirmation();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Quote generation is awaiting explicit user confirmation.", root.GetProperty("error").GetString());
        var instruction = root.GetProperty("instruction").GetString();
        Assert.NotNull(instruction);
        Assert.Contains("consent", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("generate_quote", instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeExistingQuoteReady_IncludesUiForViewQuoteAndAccept()
    {
        const string url = "https://example.org/files/quotes/Quote-123.html";
        const string booking = "250195";
        var json = QuoteGenerationToolOutput.SerializeExistingQuoteReady(url, booking);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("alreadyExists").GetBoolean());
        Assert.Equal(url, root.GetProperty("ui").GetProperty("quoteUrl").GetString());
        Assert.Equal(booking, root.GetProperty("ui").GetProperty("bookingNo").GetString());
        Assert.True(root.GetProperty("ui").GetProperty("isHtml").GetBoolean());
        Assert.Contains("View quote", root.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accept this quote", root.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("View quote and Accept", root.GetProperty("instruction").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeNewQuoteReady_HasSuccessWithoutAlreadyExistsFlag()
    {
        const string url = "https://example.org/q.html";
        const string booking = "999";
        var json = QuoteGenerationToolOutput.SerializeNewQuoteReady(url, booking);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(root.TryGetProperty("alreadyExists", out _));
        Assert.Equal(url, root.GetProperty("ui").GetProperty("quoteUrl").GetString());
    }

    [Fact]
    public void SerializeQuoteRecoveredAfterGenerationError_MarksRecoveredFromError()
    {
        var json = QuoteGenerationToolOutput.SerializeQuoteRecoveredAfterGenerationError("https://x/u", "1");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("recoveredFromError").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }
}
