using System.ComponentModel.DataAnnotations;

namespace EventHub.API.DTOs;

public record CreateFeedbackDto(
    [Required]
    int IdEvent,

    [Range(1, 5, ErrorMessage = "Rating must be an integer between 1 and 5.")]
    int Rating,

    string? Comment
);

public record UpdateFeedbackDto(
    [Range(1, 5, ErrorMessage = "Rating must be an integer between 1 and 5.")]
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