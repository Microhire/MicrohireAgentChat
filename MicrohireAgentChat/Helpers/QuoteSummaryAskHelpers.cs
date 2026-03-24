using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Helpers;

/// <summary>
/// Shared detection for "assistant asked for confirmation before creating the quote" — used by
/// <see cref="Controllers.ChatController"/> and the chat UI so server and view stay aligned.
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
    /// True when normalized assistant text is asking the user to confirm before quoting
    /// (same rules as <c>WasLastAssistantASummaryAsk</c> in ChatController).
    /// </summary>
    public static bool LooksLikeSummaryAskNormalized(string t)
    {
        if (string.IsNullOrEmpty(t))
            return false;

        return t.Contains("here is your summary")
            || t.Contains("here's your summary")
            || t.Contains("here's what i have so far")
            || t.Contains("here's a summary")
            || t.Contains("let me summarise") || t.Contains("let me summarize")
            || t.Contains("does everything look correct")
            || t.Contains("does this look correct")
            || t.Contains("please confirm")
            || t.Contains("can you confirm")
            || t.Contains("could you confirm")
            || t.Contains("before i create your quote")
            || t.Contains("before creating your quote")
            || t.Contains("before generating your quote")
            || t.Contains("before proceeding to your quote")
            || t.Contains("before i proceed")
            || t.Contains("before i generate your quote")
            || t.Contains("before i proceed to generate the quote")
            || t.Contains("are there any other details")
            || t.Contains("anything else you'd like to add")
            || t.Contains("would you like to add anything else")
            || t.Contains("is there anything else")
            || t.Contains("anything else to add")
            || t.Contains("ready to create the quote")
            || t.Contains("shall i create the quote")
            || t.Contains("shall i generate the quote")
            || t.Contains("shall we move ahead")
            || t.Contains("shall we proceed")
            || t.Contains("would you like me to create")
            || t.Contains("would you like me to generate")
            || t.Contains("finalise the quote") || t.Contains("finalize the quote")
            || t.Contains("finalised equipment") || t.Contains("finalized equipment")
            || t.Contains("equipment lineup")
            || t.Contains("equipment selection")
            || t.Contains("here's your finalised") || t.Contains("here's your finalized")
            || t.Contains("here's a finalised") || t.Contains("here's a finalized")
            || t.Contains("a finalised summary") || t.Contains("a finalized summary")
            || t.Contains("here's the equipment")
            || t.Contains("quote summary")
            || t.Contains("recommended equipment")
            || t.Contains("equipment looks correct")
            || t.Contains("i can create your quote")
            || t.Contains("create your quote now")
            || t.Contains("estimated total")
            || t.Contains("let me know if there's anything i've missed")
            || t.Contains("let me know if there is anything i've missed")
            || t.Contains("any corrections before i proceed")
            || t.Contains("recommend an av equipment package");
    }
}
