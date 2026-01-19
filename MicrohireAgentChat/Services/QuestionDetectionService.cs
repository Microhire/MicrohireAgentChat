using MicrohireAgentChat.Models;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Detects user questions and extracts context for appropriate responses
/// </summary>
public sealed class QuestionDetectionService
{
    /// <summary>
    /// Analyze user message to detect if it's a question and extract context
    /// </summary>
    public QuestionInfo? DetectQuestion(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return null;

        var message = userMessage.ToLowerInvariant().Trim();

        // Question patterns
        var questionPatterns = new[]
        {
            @"^(what|how|which|can you|could you|would you|please)\s+(is|are|should|would|can|could|do|does|did)\s+(suggested|optimal|recommended|best|good)",
            @"^(what|how|which)\s+(is|are)\s+(the\s+)?(?:suggested|optimal|recommended|best)",
            @"^(can|could|would)\s+(you\s+)?(?:suggest|recommend|tell me)",
            @"^(what's|how's)\s+(suggested|optimal|recommended|best)",
            @"^(tell me|show me)\s+(what|how)"
        };

        foreach (var pattern in questionPatterns)
        {
            if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
            {
                var questionType = DetermineQuestionType(message);
                var context = ExtractQuestionContext(message);

                return new QuestionInfo
                {
                    QuestionType = questionType,
                    Context = context,
                    OriginalQuestion = userMessage
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Determine the type of question asked
    /// </summary>
    private QuestionType DetermineQuestionType(string message)
    {
        if (Regex.IsMatch(message, @"room|venue|setup|layout|configuration"))
            return QuestionType.RoomSetup;

        if (Regex.IsMatch(message, @"equipment|microphone|projector|screen|laptop|audio|lighting"))
            return QuestionType.Equipment;

        if (Regex.IsMatch(message, @"suggested|optimal|recommended|best"))
            return QuestionType.RoomSetup; // Most common use case

        return QuestionType.General;
    }

    /// <summary>
    /// Extract context from the question (room names, equipment types, etc.)
    /// </summary>
    private QuestionContext ExtractQuestionContext(string message)
    {
        var context = new QuestionContext();

        // Extract room names
        var roomMatch = Regex.Match(message, @"(?:for|in|at)\s+(?:the\s+)?([A-Za-z\s]+?)\s+(?:room|venue)", RegexOptions.IgnoreCase);
        if (roomMatch.Success)
        {
            context.RoomName = roomMatch.Groups[1].Value.Trim();
        }

        // Extract equipment types
        var equipmentMatch = Regex.Match(message, @"(?:about|for)\s+([A-Za-z\s]+?)\s+(?:equipment|setup|configuration)", RegexOptions.IgnoreCase);
        if (equipmentMatch.Success)
        {
            context.EquipmentType = equipmentMatch.Groups[1].Value.Trim();
        }

        return context;
    }
}

/// <summary>
/// Information about a detected question
/// </summary>
public class QuestionInfo
{
    public QuestionType QuestionType { get; set; }
    public QuestionContext Context { get; set; } = new();
    public string OriginalQuestion { get; set; } = "";
}

/// <summary>
/// Type of question detected
/// </summary>
public enum QuestionType
{
    General,
    RoomSetup,
    Equipment,
    Venue
}

/// <summary>
/// Context extracted from a question
/// </summary>
public class QuestionContext
{
    public string? RoomName { get; set; }
    public string? EquipmentType { get; set; }
}