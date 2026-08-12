using EventHub.API.DTOs;

namespace EventHub.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterPersonDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    
    Task<string> ForgotPasswordAsync(ForgotPasswordDto dto);
    Task<string> ResetPasswordAsync(ResetPasswordDto dto);
}