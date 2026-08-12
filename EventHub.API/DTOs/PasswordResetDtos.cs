namespace EventHub.API.DTOs;
using System.ComponentModel.DataAnnotations;
// Request payload for /api/auth/forgot-password
public record ForgotPasswordDto(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    string Email
);

// Request payload for /api/auth/reset-password
public record ResetPasswordDto(
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        string Email,

        [Required(ErrorMessage = "Reset token is required.")]
        string Token,

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
            ErrorMessage = "Password must contain at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special symbol."
        )]
        string NewPassword
    );
