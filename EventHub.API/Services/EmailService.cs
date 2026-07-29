using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EventHub.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailWithAttachmentsAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        IEnumerable<EmailAttachmentDto> attachments)
    {
        var smtpSettings = _config.GetSection("SmtpSettings");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            smtpSettings["SenderName"] ?? "EventHub Platform", 
            smtpSettings["SenderEmail"] ?? "no-reply@eventhub.com"));
        
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = bodyHtml
        };

        // Attach each file provided in the attachments collection
        if (attachments != null)
        {
            foreach (var att in attachments)
            {
                if (!string.IsNullOrEmpty(att.PhysicalPath) && File.Exists(att.PhysicalPath))
                {
                    builder.Attachments.Add(
                        att.FileName, 
                        File.ReadAllBytes(att.PhysicalPath), 
                        ContentType.Parse(att.ContentType)
                    );
                }
            }
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        
        var host = smtpSettings["Host"] ?? "localhost";
        var port = int.Parse(smtpSettings["Port"] ?? "587");
        
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable);

        var username = smtpSettings["Username"];
        var password = smtpSettings["Password"];

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            await client.AuthenticateAsync(username, password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public Task SendEmailWithAttachmentAsync(
        string toEmail, 
        string subject, 
        string bodyHtml, 
        string attachmentPhysicalPath, 
        string attachmentFileName, 
        string contentType = "application/pdf")
    {
        var attachments = new List<EmailAttachmentDto>();

        if (!string.IsNullOrEmpty(attachmentPhysicalPath))
        {
            attachments.Add(new EmailAttachmentDto(attachmentPhysicalPath, attachmentFileName, contentType));
        }

        return SendEmailWithAttachmentsAsync(toEmail, subject, bodyHtml, attachments);
    }
}