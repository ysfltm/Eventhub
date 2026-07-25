namespace EventHub.API.Services;

public interface IInvitationPdfService
{
    string GenerateInvitationPdf(
        int participationId,
        string eventTitle,
        string companyName,
        DateTime eventDate,
        TimeSpan startTime,
        string address,
        string personName,
        string personEmail,
        string qrPayload
    );
}