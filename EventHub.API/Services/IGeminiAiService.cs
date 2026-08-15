using EventHub.API.DTOs;

namespace EventHub.API.Services;

public interface IGeminiAiService
{
    Task<GeneratedEventResponseDto> GenerateEventPlanAsync(GenerateEventPromptDto dto);
    
    Task<EventSentimentInsightsDto> AnalyzeFeedbackSentimentAsync(int eventId, string eventTitle, List<(int Rating, string Comment)> reviews);
    
    Task<EventConciergeChatResponseDto> AskEventConciergeAsync(
        int eventId,
        string eventTitle,
        string description,
        string address,
        string timeWindow,
        string dateFormatted,
        string companyName,
        string companyContact,
        EventConciergeChatRequestDto dto
    );
}