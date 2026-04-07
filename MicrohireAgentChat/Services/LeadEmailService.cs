using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Models;
using MimeKit;

namespace MicrohireAgentChat.Services;

public interface ILeadEmailService
{
    Task SendLeadNotificationAsync(WestinLead lead, string chatLink, CancellationToken ct = default);
}

public sealed class LeadEmailService : ILeadEmailService
{
    private readonly LeadEmailOptions _options;
    private readonly ILogger<LeadEmailService> _logger;

    public LeadEmailService(IOptions<LeadEmailOptions> options, ILogger<LeadEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendLeadNotificationAsync(WestinLead lead, string chatLink, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogWarning("LeadEmail not configured (SmtpHost or FromAddress missing). Skipping email for lead {LeadId}.", lead.Id);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName ?? "Microhire", _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(lead.Email));
            message.Subject = "Your Westin Brisbane event enquiry - next steps";

            var builder = new BodyBuilder { HtmlBody = BuildHtmlBody(lead, chatLink) };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, ct);

            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername) && !string.IsNullOrWhiteSpace(_options.SmtpPassword))
            {
                await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Lead notification email sent to {Email} for lead {LeadId}.", lead.Email, lead.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send lead notification email to {Email}.", lead.Email);
            throw;
        }
    }

    private static string BuildHtmlBody(WestinLead lead, string chatLink)
    {
        var firstName = System.Net.WebUtility.HtmlEncode(lead.FirstName);
        var venue = System.Net.WebUtility.HtmlEncode(lead.Venue);

        return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""><title>Your Westin Brisbane event enquiry</title></head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
  <p>Hi {firstName},</p>
  <p>We have been notified of your interest in AV Services for an event you plan to host at {venue}. The details you've shared have been used to commence a quote for you. We need a little more information in order for this quote to be finalized.</p>
  <p>Click the button below to continue your quote with Isla, our online assistant. You will be able to complete the quote independently on the spot.</p>
  <p style=""margin: 30px 0;"">
    <a href=""{chatLink}"" style=""display: inline-block; padding: 14px 28px; background-color: #ca1a20; color: white; text-decoration: none; border-radius: 8px; font-weight: 600;"">Start Quote</a>
  </p>
  <p>Once you confirm your quote, a Microhire team member will contact you shortly thereafter.</p>
  <p>Regards,<br>Microhire</p>
  <p style=""font-size: 12px; color: #999;"">If the button doesn't work, copy and paste this link into your browser:<br>{chatLink}</p>
</body>
</html>";
    }
}
