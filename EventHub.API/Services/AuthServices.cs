using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EventHub.API.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;

    public AuthService(AppDbContext context, IConfiguration configuration, IEmailService emailService)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterPersonDto dto)
    {
        var existingPerson = await _context.People.FirstOrDefaultAsync(p => p.Email == dto.Email);
        if (existingPerson != null)
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        var person = new Person
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            CompanyName = dto.CompanyName,
            Position = dto.Position,
            Role = dto.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsAccountActivated = true
        };

        _context.People.Add(person);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(person);

        return new AuthResponseDto(
            token,
            person.IdPerson,
            person.Email,
            person.Role.ToString(),
            person.FirstName,
            person.LastName
        );
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var person = await _context.People.FirstOrDefaultAsync(p => p.Email == dto.Email);

        if (person == null || string.IsNullOrEmpty(person.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!person.IsAccountActivated)
        {
            throw new InvalidOperationException("Account is not activated yet.");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, person.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var token = GenerateJwtToken(person);

        return new AuthResponseDto(
            token,
            person.IdPerson,
            person.Email,
            person.Role.ToString(),
            person.FirstName,
            person.LastName
        );
    }

    public async Task<string> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            throw new InvalidOperationException("Email address is required.");
        }

        var person = await _context.People.FirstOrDefaultAsync(p => p.Email.ToLower() == dto.Email.ToLower());

        // Security Guard: Prevent email enumeration
        if (person == null)
        {
            return "If an account with that email exists, we have sent a password reset link.";
        }

        // Generate cryptographically secure token
        byte[] randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        string token = Convert.ToHexString(randomBytes);

        // Store token and 1-hour expiration date
        person.PasswordResetToken = token;
        person.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        await _context.SaveChangesAsync();

        // Build Frontend Reset Link
        string resetLink = $"http://localhost:5173/reset-password?token={token}&email={Uri.EscapeDataString(person.Email)}";

        string htmlBody = $@"
            <div style='font-family: Arial, sans-serif; color: #333; max-width: 500px; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                <h2 style='color: #1a56db;'>🔐 Reset Your EventHub Password</h2>
                <p>Hello <strong>{person.FirstName}</strong>,</p>
                <p>We received a request to reset your password. Click the button below to set up a new password:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{resetLink}' style='background-color: #1a56db; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Reset Password</a>
                </div>
                <p style='color: #666; font-size: 0.9em;'>This link will expire in <strong>1 hour</strong>.</p>
                <p style='color: #999; font-size: 0.8em;'>If you did not request a password reset, you can safely ignore this email.</p>
            </div>";

        // Dispatch email using injected IEmailService
        await _emailService.SendEmailAsync(
            toEmail: person.Email,
            subject: "🔑 Reset Your EventHub Password",
            bodyHtml: htmlBody
        );

        return "If an account with that email exists, we have sent a password reset link.";
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            throw new InvalidOperationException("Email, token, and new password are required.");
        }

        var person = await _context.People.FirstOrDefaultAsync(p => p.Email.ToLower() == dto.Email.ToLower());

        // Validate token integrity and expiration timestamp
        if (person == null || 
            person.PasswordResetToken != dto.Token || 
            person.ResetTokenExpiresAt == null || 
            person.ResetTokenExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Invalid or expired password reset token.");
        }

        // Hash new password using BCrypt
        person.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        // Clear token fields so it cannot be re-used
        person.PasswordResetToken = null;
        person.ResetTokenExpiresAt = null;

        await _context.SaveChangesAsync();

        return "Password reset successfully! You can now log in with your new password.";
    }

    private string GenerateJwtToken(Person person)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"] 
            ?? throw new InvalidOperationException("JWT Secret key is missing in configuration."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, person.IdPerson.ToString()),
            new Claim(ClaimTypes.Email, person.Email),
            new Claim(ClaimTypes.Role, person.Role.ToString()),
            new Claim("FirstName", person.FirstName),
            new Claim("LastName", person.LastName)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(Convert.ToDouble(jwtSettings["ExpirationInHours"] ?? "8")),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}