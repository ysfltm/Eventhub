namespace EventHub.API.DTOs;

public record CheckInRequestDto(
    string QrPayload,
    int EventId
);

public record CheckInResponseDto(
    bool Success,
    string Message,
    int? ParticipationId,
    string? AttendeeName,
    DateTime? CheckInTime
);