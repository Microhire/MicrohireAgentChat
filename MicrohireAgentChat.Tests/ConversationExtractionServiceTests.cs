using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class ConversationExtractionServiceTests
{
    [Fact]
    public void ExtractContactInfo_UsesShortNameReply_AfterNamePrompt()
    {
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-3), new[] { "Hello, what is your full name?" }),
            new DisplayMessage("user", now.AddMinutes(-2), new[] { "Shanks Sake" }),
            new DisplayMessage("assistant", now.AddMinutes(-1), new[] { "Please provide your phone or email." }),
            new DisplayMessage("user", now, new[] { "shanks@testing.com, 0412659873" })
        };

        var contact = service.ExtractContactInfo(messages);

        Assert.Equal("Shanks Sake", contact.Name);
        Assert.Equal("shanks@testing.com", contact.Email);
        Assert.Equal("+61412659873", contact.PhoneE164);
    }

    [Fact]
    public void ExtractEventType_DetectsTeamOffsitePhrase()
    {
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-1), new[] { "What type of event is this?" }),
            new DisplayMessage("user", now, new[] { "This is a team offsite for leadership." })
        };

        var (eventType, _) = service.ExtractEventType(messages);

        Assert.Equal("team offsite", eventType);
    }

    [Fact]
    public void ExtractEventType_DetectsShortDirectAnswer()
    {
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-1), new[] { "Could you confirm the event type?" }),
            new DisplayMessage("user", now, new[] { "Presentation" })
        };

        var (eventType, _) = service.ExtractEventType(messages);

        Assert.Equal("presentation", eventType);
    }

    [Fact]
    public void ExtractContactInfo_CapturesPosition_FromShortReplyAfterPrompt()
    {
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-4), new[] { "Could you provide your full name?" }),
            new DisplayMessage("user", now.AddMinutes(-3), new[] { "Red Stark" }),
            new DisplayMessage("assistant", now.AddMinutes(-2), new[] { "Thank you, Red. Could you also provide either your email address or phone number so I can save your contact details?" }),
            new DisplayMessage("user", now.AddMinutes(-1), new[] { "redstart@redlinetest.com, 0452368753" }),
            new DisplayMessage("assistant", now.AddSeconds(-30), new[] { "Thank you for providing both your email and phone. Lastly, could you let me know your position or role within Red Line Corp? This is optional if you're happy to skip it." }),
            new DisplayMessage("user", now, new[] { "Owner" })
        };

        var contact = service.ExtractContactInfo(messages);

        Assert.Equal("Red Stark", contact.Name);
        Assert.Equal("redstart@redlinetest.com", contact.Email);
        Assert.Equal("+61452368753", contact.PhoneE164);
        Assert.Equal("Owner", contact.Position);
    }

    [Fact]
    public void ExtractContactInfo_RejectsAssistantNameTokens_AsCustomerName()
    {
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-2), new[] { "What is your full name?" }),
            new DisplayMessage("user", now.AddMinutes(-1), new[] { "Isla Microhire" }),
            new DisplayMessage("user", now, new[] { "client@example.com" })
        };

        var contact = service.ExtractContactInfo(messages);

        Assert.Null(contact.Name);
        Assert.Equal("client@example.com", contact.Email);
    }

    [Fact]
    public void ExtractContactInfo_RejectsAssistantNamePart_InMultiWordName()
    {
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-2), new[] { "What is your full name?" }),
            new DisplayMessage("user", now.AddMinutes(-1), new[] { "John Isla" }),
            new DisplayMessage("user", now, new[] { "john@example.com" })
        };

        var contact = service.ExtractContactInfo(messages);

        Assert.Null(contact.Name);
        Assert.Equal("john@example.com", contact.Email);
    }

    // =========================================================================
    // Projector area extraction — regression tests for the repeated-prompt loop
    // =========================================================================

    [Fact]
    public void ExtractProjectorAreas_DoesNotMatchBareLetterWithoutPromptContext()
    {
        // A user message containing a standalone "A" should NOT be captured as a
        // projector area when the assistant never asked about projector placement.
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-2), new[] { "What type of event is this?" }),
            new DisplayMessage("user", now, new[] { "A corporate conference." }),
        };

        var areas = service.ExtractProjectorAreas(messages);
        Assert.Empty(areas);
    }

    [Fact]
    public void ExtractProjectorAreas_MatchesBareLetterWhenProjectorPromptIsPresent()
    {
        // After the assistant shows the floor-plan prompt, a bare-letter reply like "A"
        // should be accepted as a valid projector area selection.
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-2), new[]
            {
                "Before I create the quote, please choose the **projector placement area** for Westin Ballroom.\n![Floor plan](/images/westin/westin-ballroom/floor-plan.png)"
            }),
            new DisplayMessage("user", now, new[] { "A" }),
        };

        var areas = service.ExtractProjectorAreas(messages);
        Assert.Single(areas);
        Assert.Equal("A", areas[0]);
    }

    [Fact]
    public void ExtractProjectorAreas_MatchesMultipleAreasAfterPrompt()
    {
        // "A, D" in response to the projector prompt should yield two areas.
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-2), new[]
            {
                "Please choose 2 projector placement areas for Westin Ballroom.\n![Floor plan](/images/westin/westin-ballroom/floor-plan.png)"
            }),
            new DisplayMessage("user", now, new[] { "A and D" }),
        };

        var areas = service.ExtractProjectorAreas(messages);
        Assert.Equal(2, areas.Count);
        Assert.Contains("A", areas, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("D", areas, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractProjectorAreas_AlwaysMatchesExplicitAreaKeyword()
    {
        // "area D" is unambiguous and should match even without a prior prompt.
        var service = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("user", now, new[] { "Please put the projector at area D." }),
        };

        var areas = service.ExtractProjectorAreas(messages);
        Assert.Contains("D", areas, StringComparer.OrdinalIgnoreCase);
    }
}
