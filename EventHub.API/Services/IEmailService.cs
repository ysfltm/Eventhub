namespace EventHub.API.Services;

public record EmailAttachmentDto(
    string PhysicalPath, 
    string FileName, 
    string ContentType = "application/pdf"
);

public interface IEmailService
{
   ///no attachement
    Task SendEmailAsync(string toEmail, string subject, string bodyHtml);    
    /// Sends an email with multiple attachments.
    
    Task SendEmailWithAttachmentsAsync(
        string toEmail, 
        string subject, 
        string bodyHtml, 
        IEnumerable<EmailAttachmentDto> attachments);

    
    /// Sends an email with a single attachment.
    
    Task SendEmailWithAttachmentAsync(
        string toEmail, 
        string subject, 
        string bodyHtml, 
        string attachmentPhysicalPath, 
        string attachmentFileName, 
        string contentType = "application/pdf");
}