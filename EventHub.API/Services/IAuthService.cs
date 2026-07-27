using EventHub.API.DTOs;

namespace EventHub.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterPersonDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
}