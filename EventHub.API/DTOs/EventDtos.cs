namespace EventHub.API.DTOs;

public record CreateEventDto(
    int IdCompany,
    string Title,
    string? Description,
    string Address,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? Person
);

public record EventResponseDto(
    int IdEvent,
    int IdCompany,
    string CompanyName,
    string Title,
    string? Description,
    string Address,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Status,
    string? Person,
    string? ProgramPath
);