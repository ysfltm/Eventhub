namespace EventHub.API.DTOs;

public record EventAnalyticsDto(
    int IdEvent,
    string EventTitle,
    int TotalRegistrations,
    int TotalCheckedIn,
    double AttendanceRatePercentage,
    double AverageRating,
    int TotalFeedbacks
);

public record OverallPlatformStatsDto(
    int TotalEvents,
    int TotalUsers,
    int TotalParticipations,
    int TotalCheckedIn,
    double GlobalAttendanceRatePercentage,
    double GlobalAverageRating
);