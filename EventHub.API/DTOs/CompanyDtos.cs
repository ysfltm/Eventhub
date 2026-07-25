namespace EventHub.API.DTOs;

public record CreateCompanyDto(
    string Name,
    string Email,
    string Phone,
    string? Address,
    string? Expertise,
    string? Logo,
    string? Website
);

public record CompanyResponseDto(
    int IdCompany,
    string Name,
    string Email,
    string Phone,
    string? Address,
    string? Expertise,
    string? Logo,
    string? Website
);