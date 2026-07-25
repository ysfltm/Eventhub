namespace EventHub.API.DTOs;

public record CreatePersonDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? Address,
    string? CompanyName,
    string? Position
);

public record PersonResponseDto(
    int IdPerson,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? Address,
    string? CompanyName,
    string? Position
);