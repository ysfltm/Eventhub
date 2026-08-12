namespace EventHub.API.DTOs;

using System.ComponentModel.DataAnnotations;
using EventHub.API.Models;

public record RegisterPersonDto(
    [Required(ErrorMessage = "First name is required.")]
    string FirstName,

    [Required(ErrorMessage = "Last name is required.")]
    string LastName,

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    string Email,

    string Phone,

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
        ErrorMessage = "Password must contain at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special symbol."
    )]
    string Password,

    string? Address,
    string? CompanyName,
    string? Position,

    [Required(ErrorMessage = "Role is required.")]
    PersonRole Role = PersonRole.Attendee
);

public record LoginDto(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    string Email,

    [Required(ErrorMessage = "Password is required.")]
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