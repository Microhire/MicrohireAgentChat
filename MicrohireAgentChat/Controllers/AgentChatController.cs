using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace MicrohireAgentChat.Controllers;

public sealed class AgentChatController : Controller
{
    private readonly AzureAgentChatService _svc;
    private readonly IHttpContextAccessor _http;

    public AgentChatController(AzureAgentChatService svc, IHttpContextAccessor http)
    {
        _svc = svc;
        _http = http;
    }

    [HttpGet("/agentchat")]
    public IActionResult HistoryLookup()
        => View("History", new AgentChatHistoryViewModel());

    [HttpGet("/agentchat/history")]
    public async Task<IActionResult> History([FromQuery] string? userKey, CancellationToken ct)
    {
        var vm = new AgentChatHistoryViewModel { UserKey = userKey };

        if (string.IsNullOrWhiteSpace(userKey))
        {
            vm.Error = "Enter a userKey to load the conversation.";
            return View("History", vm);
        }

        try
        {
            var data = await _svc.GetTranscriptForUserAsync(userKey, ct);
            if (data is null)
            {
                vm.Error = "No saved thread for this userKey.";
                return View("History", vm);
            }

            vm.ThreadId = data.Value.ThreadId;
            vm.Messages = data.Value.Messages;

            var ext = _svc.ExtractVenueAndEventDate(vm.Messages);
            vm.EventDate = ext.EventDate;
            vm.EventDateMatchedText = ext.DateMatched;
            vm.VenueName = ext.VenueName;
            vm.VenueMatchedText = ext.VenueMatched;
            var (date, matched) = _svc.ExtractEventDate(vm.Messages);
            vm.EventDate = date;
            vm.EventDateMatchedText = matched;
            var et = _svc.ExtractEventType(vm.Messages);
            vm.EventType = et.EventType;
            vm.EventTypeMatchedText = et.Matched;
            var ci = _svc.ExtractContactInfo(vm.Messages);
            vm.ContactName = ci.Name;
            vm.ContactNameMatchedText = ci.NameMatched;
            vm.Email = ci.Email;
            vm.EmailMatchedText = ci.EmailMatched;
            vm.Phone = ci.PhoneE164;
            vm.PhoneMatchedText = ci.PhoneMatched;

            return View("History", vm);
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
            return View("History", vm);
        }
    }

}
