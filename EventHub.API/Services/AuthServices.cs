using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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
            Role = dto.Role, // 👈 Now assigns PersonRole Enum directly
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
            person.Role.ToString(), // 👈 Converted to string for DTO payload
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
            person.Role.ToString(), // 👈 Converted to string for DTO payload
            person.FirstName,
            person.LastName
        );
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
            new Claim(ClaimTypes.Role, person.Role.ToString()), // 👈 Formats Enum to String (e.g., "Attendee")
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