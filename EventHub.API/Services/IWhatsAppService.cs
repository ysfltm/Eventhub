namespace EventHub.API.Services;

public interface IWhatsAppService
{
    // initial message 
    Task<bool> SendInvitationWhatsAppAsync(string toPhoneNumber, string attendeeName, string eventTitle);
    /// <summary>
    /// Sends a PDF document (Access Pass / Program) directly via Meta WhatsApp Cloud API.
    /// </summary>
    Task<bool> SendPassPdfWhatsAppAsync(
        string toPhoneNumber, 
        string pdfPublicUrl, 
        string fileName, 
        string caption);
}