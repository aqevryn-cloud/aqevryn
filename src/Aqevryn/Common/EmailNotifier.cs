using System.Net;
using System.Net.Mail;

namespace Aqevryn.Common;

/// <summary>
/// Sends email notifications via SMTP.
/// Configured via environment variables for the aqevryn@gmail.com account.
/// </summary>
public class EmailNotifier
{
    private readonly string? _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _smtpUser;
    private readonly string? _smtpPass;
    private readonly string? _fromAddress;

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_smtpHost) && !string.IsNullOrEmpty(_smtpUser) && !string.IsNullOrEmpty(_smtpPass);

    public EmailNotifier()
    {
        _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
        _smtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
        _smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
        _smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS");
        _fromAddress = _smtpUser;
    }

    /// <summary>Send a notification about a new Pull Request.</summary>
    public async Task SendPrNotificationAsync(string topic, string prUrl, double editorialScore, string articleTitle)
    {
        if (!IsConfigured) return;

        try
        {
            var subject = $"[Aqevryn] New Research Published: {topic}";
            var body = $@"Aqevryn has completed a new research article.

Topic: {topic}
Title: {articleTitle}
Editorial Score: {editorialScore}
Pull Request: {prUrl}

Please review the article and merge if approved.

---
Sent by Aqevryn Research Agent
https://github.com/aqevryn-cloud/aqevryn";

            using var message = new MailMessage
            {
                From = new MailAddress(_fromAddress!, "Aqevryn Research"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };
            message.To.Add(new MailAddress(_fromAddress!)); // Send to self (aqevryn@gmail.com)

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true,
                Timeout = 15000,
            };

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to send email notification: {ex.Message}");
        }
    }
}