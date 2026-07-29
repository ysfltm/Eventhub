namespace EventHub.API.Services;

public record EmailAttachmentDto(
    string PhysicalPath, 
    string FileName, 
    string ContentType = "application/pdf"
);

public interface IEmailService
{
    /// <summary>
    /// Sends an email with multiple attachments.
    /// </summary>
    Task SendEmailWithAttachmentsAsync(
        string toEmail, 
        string subject, 
        string bodyHtml, 
        IEnumerable<EmailAttachmentDto> attachments);

    /// <summary>
    /// Sends an email with a single attachment.
    /// </summary>
    Task SendEmailWithAttachmentAsync(
        string toEmail, 
        string subject, 
        string bodyHtml, 
        string attachmentPhysicalPath, 
        string attachmentFileName, 
        string contentType = "application/pdf");
}