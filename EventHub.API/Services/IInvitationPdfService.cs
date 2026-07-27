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
    string GenerateEventProgramPdf(
        int eventId,
        string eventTitle,
        string? description,
        string companyName,
        DateTime eventDate,
        TimeSpan startTime,
        TimeSpan endTime,
        string address,
        string? spokesperson,
        List<string>? sponsors = null
    );
}