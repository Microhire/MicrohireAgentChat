# Lead Email Setup (SMTP)

The Westin lead flow sends emails to customers with a chat link. Configure `LeadEmail` in `appsettings.json` (or `appsettings.Development.json` for local dev).

## Quick local setup (Gmail)

1. Copy `appsettings.Development.Local.json.example` to `appsettings.Development.Local.json`
2. Edit `appsettings.Development.Local.json` and replace `your-email@gmail.com` with your Gmail address
3. Replace `SmtpPassword` with your Gmail App Password (16 chars, no spaces)

The local file is gitignored and overrides `appsettings.Development.json`.

## Gmail SMTP (Free)

1. **Enable 2-Step Verification** on your Google Account: [Google Account Security](https://myaccount.google.com/security)
2. **Create an App Password**: [App Passwords](https://myaccount.google.com/apppasswords)
   - Select app: Mail
   - Select device: Other → name it "Microhire Lead Email"
   - Copy the 16-character password
3. **Configure** `appsettings.Development.json` (or `appsettings.json`):

```json
"LeadEmail": {
  "ChatBaseUrl": "http://localhost:5216",
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "FromAddress": "your-email@gmail.com",
  "FromName": "Microhire",
  "SmtpUsername": "your-email@gmail.com",
  "SmtpPassword": "your-16-char-app-password"
}
```

- **Local dev**: `ChatBaseUrl` = `http://localhost:5216`
- **Production**: `ChatBaseUrl` = your public chat URL (e.g. `https://microhire-xxx.azurewebsites.net`)

> ⚠️ Use an **App Password**, not your regular Gmail password. Regular passwords will fail with "Username and Password not accepted".

---

## Azure SMTP Options (Paid / Higher Volume)

### Option A: SendGrid (Azure Marketplace)

- **Free tier**: 100 emails/day
- [SendGrid on Azure](https://azure.microsoft.com/en-us/products/communication-services/sendgrid/)
- Configure: `SmtpHost` = `smtp.sendgrid.net`, port 587, username `apikey`, password = your SendGrid API key

### Option B: Azure Communication Services Email

- Requires code changes (use ACS SDK instead of SMTP)
- [ACS Email](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email)

### Option C: Office 365 / Microsoft 365

- If you have M365: `smtp.office365.com`, port 587, your work email + password (or app password if MFA enabled)

---

## Config Reference

| Setting       | Description                          | Example                    |
|---------------|--------------------------------------|----------------------------|
| ChatBaseUrl   | Public chat URL for links in email   | `http://localhost:5216`    |
| SmtpHost      | SMTP server                          | `smtp.gmail.com`           |
| SmtpPort      | Usually 587 (StartTLS)                | `587`                      |
| FromAddress   | Sender email                         | `noreply@yourdomain.com`   |
| FromName      | Sender display name                  | `Microhire`                |
| SmtpUsername  | SMTP login (often same as FromAddress) | `your@gmail.com`         |
| SmtpPassword  | SMTP password or App Password        | *(secret)*                 |
