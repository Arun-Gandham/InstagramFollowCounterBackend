using FollowerCounter.Application.DTOs.Instagram;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IInstagramService
{
    Task<InstagramConnectResponseDto> InitiateConnectAsync(Guid userId, string? redirectAfterSuccess, string? ipAddress, CancellationToken cancellationToken = default);

    Task<string> HandleCallbackAsync(string code, string state, string? ipAddress, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstagramAccountDto>> GetAccountsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<InstagramAccountDto> GetAccountByIdAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);

    Task<RefreshFollowerResultDto> ManualRefreshAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);

    Task DisconnectAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default);
}
