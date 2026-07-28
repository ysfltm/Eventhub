namespace EventHub.API.DTOs;

public record CreateFeedbackDto(
    int IdEvent,
    int Rating,
    string? Comment
);

public record UpdateFeedbackDto(
    int Rating,
    string? Comment
);

public record FeedbackResponseDto(
    int IdFeedback,
    int IdParticipation,
    int IdEvent,
    string EventTitle,
    int IdPerson,
    string PersonName,
    int Rating,
    string? Comment,
    DateTime CreatedAt
);

public record EventRatingSummaryDto(
    int IdEvent,
    string EventTitle,
    double AverageRating,
    int TotalFeedbacks
);