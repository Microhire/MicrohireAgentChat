using System.Text.RegularExpressions;
using MicrohireAgentChat.Models;

namespace MicrohireAgentChat.Helpers;

/// <summary>
/// Shared detection for quote-generation prompts and legacy summary-ask hooks. Pre-quote equipment summary
/// is removed; <see cref="LooksLikeSummaryAskNormalized"/> stays false. <see cref="LooksLikeQuoteGenerationPromptNormalized"/>
/// still aligns conversational consent and wizard skip logic when the model asks to proceed to quoting.
/// </summary>
public static class QuoteSummaryAskHelpers
{
    public static string NormalizeForSummaryAsk(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;
        s = s.ToLowerInvariant();
        s = s.Replace('’', '\'').Replace('‘', '\'')
            .Replace('“', '"').Replace('”', '"')
            .Replace('–', '-').Replace('—', '-');
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Legacy pre-quote “equipment summary / create quote now?” detection. The product no longer shows that step;
    /// kept for API stability and always returns false so chat CTAs and consent gating do not treat old copy as a summary ask.
    /// </summary>
    public static bool LooksLikeSummaryAskNormalized(string t) => false;

    /// <summary>
    /// Narrow match: assistant is steering toward <em>quote generation</em> (create/generate quote, not post-PDF acceptance).
    /// Used for conversational consent to unlock <c>generate_quote</c> and to skip late wizard injections.
    /// Do not use phrases like "please confirm" here — they match venue/email intake and break form injection.
    /// </summary>
    public static bool LooksLikeQuoteGenerationPromptNormalized(string t)
    {
        if (string.IsNullOrEmpty(t))
            return false;

        return t.Contains("proceed with generating")
            || t.Contains("recommendations and quote")
            || t.Contains("would you like me to create the quote now")
            || (t.Contains("would you like me to create") && t.Contains("quote"))
            || (t.Contains("would you like me to generate") && t.Contains("quote"))
            || t.Contains("before i create your quote")
            || t.Contains("before creating your quote")
            || t.Contains("before generating your quote")
            || t.Contains("before proceeding to your quote")
            || t.Contains("before i generate your quote")
            || t.Contains("before i proceed to generate the quote")
            || t.Contains("ready to create the quote")
            || t.Contains("shall i create the quote")
            || t.Contains("shall i generate the quote")
            || t.Contains("finalise the quote")
            || t.Contains("finalize the quote")
            || t.Contains("create your quote now")
            || t.Contains("i can create your quote")
            || t.Contains("equipment looks correct")
            || t.Contains("estimated total")
            || t.Contains("any corrections before i proceed")
            || t.Contains("recommend an av equipment package");
    }

    /// <summary>
    /// True when any assistant message is asking to move to quote generation (narrow — see <see cref="LooksLikeQuoteGenerationPromptNormalized"/>).
    /// </summary>
    public static bool AssistantMessageContainsQuoteGenerationPrompt(DisplayMessage? message)
    {
        if (message == null || !string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;

        var raw = string.Join("\n\n", message.Parts ?? Enumerable.Empty<string>());
        if (string.IsNullOrWhiteSpace(raw))
            raw = message.FullText ?? string.Empty;
        var t = NormalizeForSummaryAsk(raw);
        return LooksLikeQuoteGenerationPromptNormalized(t);
    }

    /// <summary>
    /// True when the assistant message body (after normalization) matches a pre-quote confirmation ask.
    /// </summary>
    public static bool AssistantMessageContainsSummaryAsk(DisplayMessage? message)
    {
        if (message == null || !string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;

        var raw = string.Join("\n\n", message.Parts ?? Enumerable.Empty<string>());
        if (string.IsNullOrWhiteSpace(raw))
            raw = message.FullText ?? string.Empty;
        var t = NormalizeForSummaryAsk(raw);
        return LooksLikeSummaryAskNormalized(t);
    }

    /// <summary>
    /// Wizard-injected read-only confirmation cards (submittedForm JSON) — not a conversational summary ask.
    /// </summary>
    public static bool IsAssistantSubmittedFormUiMessage(DisplayMessage? message)
    {
        if (message == null || !string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;

        const string marker = "\"type\":\"submittedForm\"";
        foreach (var part in message.Parts ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(part) && part.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return !string.IsNullOrWhiteSpace(message.FullText)
            && message.FullText.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }
}
