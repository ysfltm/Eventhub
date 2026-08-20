namespace EventHub.API.DTOs;

public record CreateParticipationDto(
    int IdEvent,
    int IdPerson,
    string Type = "Attendee",
    string Status = "Invited"
);

public record ParticipationResponseDto(
    int IdParticipation,
    int IdEvent,
    string EventTitle,
    int IdPerson,
    string PersonName,
    string PersonEmail,
    string Type,
    string Status,
    DateTime InvitationDate,
    DateTime? CheckInTime,
    DateTime? CheckOutTime
);
public record UpdateParticipationStatusDto(
    string Status // "Pending" | "Confirmed" | "Cancelled" | "CheckedIn" | "invited"
);