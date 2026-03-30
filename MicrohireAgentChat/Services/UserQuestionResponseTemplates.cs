namespace MicrohireAgentChat.Services;

/// <summary>
/// Fallback assistant text when the user asks clarifying questions during the booking flow.
/// Used by <see cref="AzureAgentChatService"/> so behaviour is covered by unit tests.
/// </summary>
internal static class UserQuestionResponseTemplates
{
    internal const string RoomSetupPromptNeedRoom =
        "For room setup suggestions, I can help you choose the optimal configuration. What room will you be using?";

    internal const string EquipmentGuidance =
        "For equipment recommendations, I can suggest the best setup for your event. Let me ask a few questions about your needs first.";

    internal const string GeneralGatherEventInfo =
        "I'd be happy to help answer your question. Let me gather some information about your event first.";
}
