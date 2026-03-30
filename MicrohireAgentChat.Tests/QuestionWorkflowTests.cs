using MicrohireAgentChat.Services;

namespace MicrohireAgentChat.Tests;

/// <summary>
/// Validates detection of user questions and template responses used when the AI
/// answers / guides the user (aligned with <see cref="AzureAgentChatService"/> question handling).
/// </summary>
public sealed class QuestionWorkflowTests
{
    [Fact]
    public void UserQuestionResponseTemplates_AreNonEmptyGuidanceStrings()
    {
        Assert.False(string.IsNullOrWhiteSpace(UserQuestionResponseTemplates.RoomSetupPromptNeedRoom));
        Assert.False(string.IsNullOrWhiteSpace(UserQuestionResponseTemplates.EquipmentGuidance));
        Assert.False(string.IsNullOrWhiteSpace(UserQuestionResponseTemplates.GeneralGatherEventInfo));
        Assert.Contains("room", UserQuestionResponseTemplates.RoomSetupPromptNeedRoom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("equipment", UserQuestionResponseTemplates.EquipmentGuidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuestionDetectionService_DetectsEquipmentQuestion()
    {
        var svc = new QuestionDetectionService();
        // Avoid the word "setup" so RoomSetup is not chosen before Equipment.
        var info = svc.DetectQuestion("Could you recommend the best wireless microphone for a panel discussion?");
        Assert.NotNull(info);
        Assert.Equal(QuestionType.Equipment, info!.QuestionType);
    }

    [Fact]
    public void QuestionDetectionService_DetectsRoomSetupQuestion()
    {
        var svc = new QuestionDetectionService();
        var info = svc.DetectQuestion("What is the recommended room layout for 80 people?");
        Assert.NotNull(info);
        Assert.Equal(QuestionType.RoomSetup, info!.QuestionType);
    }
}
