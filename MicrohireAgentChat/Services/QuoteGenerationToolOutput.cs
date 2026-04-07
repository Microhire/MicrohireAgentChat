using System.Text.Json;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Canonical JSON payloads returned to the agent for <c>generate_quote</c> tool outputs.
/// Centralized for tests and for use from <see cref="AzureAgentChatService"/> tool loop.
/// </summary>
internal static class QuoteGenerationToolOutput
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null
    };

    /// <summary>User has not yet consented (text/button confirmation workflow).</summary>
    internal static string SerializeAwaitingExplicitUserConfirmation() =>
        JsonSerializer.Serialize(new
        {
            error = "Quote generation is awaiting explicit user confirmation.",
            instruction =
                "Quote generation is locked until the user consents (e.g. they say 'yes create quote' / 'generate the quote') or submits structured follow-up AV (Generate quote). Do not ask for a long equipment summary; when requirements are complete, ask one short consent line if needed, then call generate_quote after they agree."
        }, SerializerOptions);

    private static string ViewQuoteMessage(string bookingNo) =>
        $"Great news! I have successfully generated your quote for booking {bookingNo}.";

    private static readonly string ViewQuoteInstruction =
        "OUTPUT ONLY the message field (no extra text). The UI already provides View quote and Accept actions; do not add download links or duplicate URLs.";

    /// <summary>Quote already exists; UI shows View quote / Accept — agent must not duplicate URLs.</summary>
    internal static string SerializeExistingQuoteReady(string fullQuoteUrl, string bookingNo) =>
        JsonSerializer.Serialize(new
        {
            ui = new
            {
                quoteUrl = fullQuoteUrl,
                bookingNo,
                isHtml = true
            },
            message = ViewQuoteMessage(bookingNo),
            alreadyExists = true,
            success = true,
            instruction = ViewQuoteInstruction
        }, SerializerOptions);

    /// <summary>Newly generated HTML quote (first success path).</summary>
    internal static string SerializeNewQuoteReady(string fullQuoteUrl, string bookingNo) =>
        JsonSerializer.Serialize(new
        {
            ui = new
            {
                quoteUrl = fullQuoteUrl,
                bookingNo,
                isHtml = true
            },
            message = ViewQuoteMessage(bookingNo),
            success = true,
            instruction = ViewQuoteInstruction
        }, SerializerOptions);

    /// <summary>Generation failed but session still has a completed quote — same UI contract as existing quote.</summary>
    internal static string SerializeQuoteRecoveredAfterGenerationError(string fullQuoteUrl, string bookingNo) =>
        JsonSerializer.Serialize(new
        {
            ui = new
            {
                quoteUrl = fullQuoteUrl,
                bookingNo,
                isHtml = true
            },
            message = ViewQuoteMessage(bookingNo),
            recoveredFromError = true,
            success = true,
            instruction = ViewQuoteInstruction
        }, SerializerOptions);
}
