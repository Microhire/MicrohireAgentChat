using Microsoft.AspNetCore.Mvc;
using MicrohireAgentChat.Services;

namespace MicrohireAgentChat.Controllers;

public sealed class ChatAdminController : Controller
{
    private readonly AzureAgentChatService _agent;

    public ChatAdminController(AzureAgentChatService agent)
    {
        _agent = agent;
    }

    // GET /ChatAdmin/Inspect?threadId=...
    [HttpGet]
    public IActionResult Inspect(string? threadId)
    {
        var vm = new InspectVm { ThreadId = threadId ?? "" };

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            try
            {
                vm.Items = _agent.ReadChat(threadId);
                vm.Details = _agent.ExtractConversationDetails(threadId);
                vm.Images = _agent.GetLatestAssistantImageUrls(threadId, galleryLookback: 3);
            }
            catch (Exception ex)
            {
                vm.Error = ex.Message;
            }
        }

        return View(vm);
    }
}

public sealed class InspectVm
{
    public string ThreadId { get; set; } = "";
    public string? Error { get; set; }

    public IReadOnlyList<ChatItem> Items { get; set; } = Array.Empty<ChatItem>();
    public ConversationDetails? Details { get; set; }
    public IReadOnlyList<string> Images { get; set; } = Array.Empty<string>();
}
