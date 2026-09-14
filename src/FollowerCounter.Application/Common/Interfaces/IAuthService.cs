using FollowerCounter.Application.DTOs.Auth;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task VerifyEmailAsync(VerifyEmailRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task ForgotPasswordAsync(ForgotPasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(Guid userId, DeleteAccountRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);
}
