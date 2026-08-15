namespace EventHub.API.DTOs;

// --- Event Creation AI DTOs ---
public record GenerateEventPromptDto(
    string Topic,
    string? Category = "Technology",
    string? Language = "en", // "en", "fr", "ar"
    int? DurationHours = 6,
    string? TargetAudience = "Professionals & Developers",
    string? LocationPreference = "Tunis Convention Center",
    int? PreferredCapacity = 150
);

public record AgendaSessionDto(
    string Time,
    string Title,
    string SpeakerRole,
    string Description
);

public record GeneratedEventResponseDto(
    string Title,
    string Description,
    string Category,
    string Address,
    int Capacity,
    string StartTime, // "09:00"
    string EndTime,   // "17:00"
    List<AgendaSessionDto> AgendaSchedule,
    List<string> KeyTakeaways,
    bool IsAiGenerated,
    bool IsFallback
);

// --- Feedback Sentiment AI DTOs ---
public record EventSentimentInsightsDto(
    int TotalReviewsAnalyzed,
    double AverageRating,
    double SentimentScorePercent,
    string SentimentLabel, // "Very Positive", "Positive", "Mixed", "Needs Improvement"
    string ExecutiveSummary,
    List<string> TopStrengths,
    List<string> AreasForImprovement,
    bool IsFallback
);

// --- Attendee AI Concierge Chat DTOs ---
public record ChatMessageDto(string Role, string Content); // Role: "user" | "model"

public record EventConciergeChatRequestDto(
    string Message,
    List<ChatMessageDto>? History,
    string? Language = "en"
);

public record EventConciergeChatResponseDto(
    string Reply,
    List<string>? SuggestedFollowUps,
    bool IsFallback
);