using EventHub.API.Models;

namespace EventHub.API.DTOs;

public record CreatePersonDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    int? IdCompany, 
    string? Address,
    string? CompanyName,
    string? Position,
    string? LinkedInUrl,
    PersonRole Role = PersonRole.Attendee
);

public record PersonResponseDto(
    int IdPerson,
    int? IdCompany, 
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? Address,
    string? CompanyName,
    string? Position, 
    string? LinkedInUrl,
    PersonRole Role
);
// Bulk employee upload DTO
public record BulkCreatePersonDto(
    int IdCompany,
    List<CreatePersonDto> Employees
);