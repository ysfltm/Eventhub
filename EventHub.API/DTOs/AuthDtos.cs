namespace EventHub.API.DTOs;
using EventHub.API.Models;
public record RegisterPersonDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password,
    string? Address,
    string? CompanyName,
    string? Position,
    PersonRole Role = PersonRole.Attendee
);

public record LoginDto(
    string Email,
    string Password
);

public record AuthResponseDto(
    string Token,
    int IdPerson,
    string Email,
    string Role,
    string FirstName,
    string LastName
);