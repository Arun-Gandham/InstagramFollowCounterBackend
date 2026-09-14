using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Instagram;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Domain.Exceptions;
using FollowerCounter.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FollowerCounter.Infrastructure.Services;

public class InstagramService : IInstagramService
{
    private readonly IAppDbContext _dbContext;
    private readonly IInstagramProvider _instagramProvider;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ISecurityEventService _securityEventService;
    private readonly ILogger<InstagramService> _logger;

    public InstagramService(
        IAppDbContext dbContext,
        IInstagramProvider instagramProvider,
        ISecretProtector _secretProtector,
        IAuditLogService auditLogService,
        ISecurityEventService securityEventService,
        ILogger<InstagramService> logger)
    {
        _dbContext = dbContext;
        _instagramProvider = instagramProvider;
        this._secretProtector = _secretProtector;
        _auditLogService = auditLogService;
        _securityEventService = securityEventService;
        _logger = logger;
    }

    public async Task<InstagramConnectResponseDto> InitiateConnectAsync(Guid userId, string? redirectAfterSuccess, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var rawState = CryptoHelper.GenerateSecureToken(32);
        var stateHash = CryptoHelper.ComputeSha256Hash(rawState);
        var ipHash = !string.IsNullOrWhiteSpace(ipAddress) ? CryptoHelper.ComputeSha256Hash(ipAddress) : null;

        var session = new InstagramOAuthSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StateHash = stateHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10), // 10 minute window
            RedirectAfterSuccess = redirectAfterSuccess,
            IpHash = ipHash
        };

        _dbContext.InstagramOAuthSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var authUrl = await _instagramProvider.BuildAuthorizationUrlAsync(rawState, cancellationToken);
        return new InstagramConnectResponseDto(authUrl);
    }

    public async Task<string> HandleCallbackAsync(string code, string state, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            throw new DomainException("Authorization code and state must be provided.");
        }

        var stateHash = CryptoHelper.ComputeSha256Hash(state);
        var session = await _dbContext.InstagramOAuthSessions
            .FirstOrDefaultAsync(s => s.StateHash == stateHash, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (session == null)
        {
            await _securityEventService.RecordEventAsync("OAuthStateNotFound", "High", ipAddress, details: new { stateHash }, cancellationToken: cancellationToken);
            throw new DomainException("Invalid Instagram OAuth state token.");
        }

        if (!session.IsValid(now))
        {
            var reason = session.UsedAt.HasValue ? "ReplayAttackAttempt" : "StateExpired";
            await _securityEventService.RecordEventAsync(reason, "High", ipAddress, details: new { session.UserId, session.ExpiresAt, session.UsedAt }, cancellationToken: cancellationToken);
            throw new DomainException("Instagram OAuth session has already been used or has expired.");
        }

        // Single-use invalidation
        session.MarkUsed(now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Server-to-server token exchange
        var tokenResult = await _instagramProvider.ExchangeAuthorizationCodeAsync(code, cancellationToken);
        var profile = await _instagramProvider.GetCurrentProfileAsync(tokenResult.AccessToken, cancellationToken);

        var encryptedToken = _secretProtector.Encrypt(tokenResult.AccessToken);

        // Check if account already exists for this user and Instagram account
        var account = await _dbContext.InstagramAccounts
            .FirstOrDefaultAsync(a => a.OwnerUserId == session.UserId && a.InstagramUserId == profile.Id, cancellationToken);

        var isNewAccount = false;
        if (account == null)
        {
            account = new InstagramAccount
            {
                Id = Guid.NewGuid(),
                OwnerUserId = session.UserId,
                InstagramUserId = profile.Id,
                FollowerSequence = 0,
                CreatedAt = now
            };
            _dbContext.InstagramAccounts.Add(account);
            isNewAccount = true;
        }

        account.Username = profile.Username;
        account.AccountType = profile.AccountType;
        account.ConnectionStatus = InstagramConnectionStatus.Connected;
        account.TokenEncrypted = encryptedToken;
        account.TokenIssuedAt = tokenResult.IssuedAt;
        account.TokenExpiresAt = tokenResult.ExpiresAt;
        account.TokenType = tokenResult.TokenType;
        account.Scopes = tokenResult.Scopes ?? "instagram_business_basic";
        account.RequiresReauthorization = false;
        account.LastApiErrorCode = null;
        account.LastApiErrorAt = null;
        account.TokenRefreshFailureCount = 0;
        account.UpdatedAt = now;

        if (profile.FollowersCount.HasValue)
        {
            if (isNewAccount)
            {
                account.FollowerCount = profile.FollowersCount.Value;
                account.PreviousFollowerCount = profile.FollowersCount.Value;
                account.FollowerSequence = 0;
                account.LastFollowerRefreshAt = now;
                account.LastSuccessfulApiCallAt = now;

                var history = new FollowerHistory
                {
                    Id = Guid.NewGuid(),
                    InstagramAccountId = account.Id,
                    OldCount = profile.FollowersCount.Value,
                    NewCount = profile.FollowersCount.Value,
                    ChangedAt = now
                };
                _dbContext.FollowerHistories.Add(history);
            }
            else
            {
                var changed = account.TryUpdateFollowers(profile.FollowersCount.Value, now);
                if (changed)
                {
                    var history = new FollowerHistory
                    {
                        Id = Guid.NewGuid(),
                        InstagramAccountId = account.Id,
                        OldCount = account.PreviousFollowerCount ?? profile.FollowersCount.Value,
                        NewCount = profile.FollowersCount.Value,
                        ChangedAt = now
                    };
                    _dbContext.FollowerHistories.Add(history);
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync("InstagramConnected", "Success", session.UserId, ipAddress: ipAddress, metadata: new { account.InstagramUserId, account.Username }, cancellationToken: cancellationToken);

        var redirectTarget = !string.IsNullOrWhiteSpace(session.RedirectAfterSuccess)
            ? session.RedirectAfterSuccess
            : "/instagram/connected?status=success";

        return redirectTarget;
    }

    public async Task<IReadOnlyList<InstagramAccountDto>> GetAccountsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await _dbContext.InstagramAccounts
            .Where(a => a.OwnerUserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts.Select(MapToDto).ToList();
    }

    public async Task<InstagramAccountDto> GetAccountByIdAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.InstagramAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerUserId == userId, cancellationToken);

        if (account == null)
        {
            throw new NotFoundException(nameof(InstagramAccount), accountId);
        }

        return MapToDto(account);
    }

    public async Task<RefreshFollowerResultDto> ManualRefreshAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.InstagramAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerUserId == userId, cancellationToken);

        if (account == null)
        {
            throw new NotFoundException(nameof(InstagramAccount), accountId);
        }

        if (string.IsNullOrWhiteSpace(account.TokenEncrypted) || account.ConnectionStatus == InstagramConnectionStatus.Disconnected)
        {
            throw new DomainException("Instagram account is disconnected.");
        }

        var now = DateTimeOffset.UtcNow;
        var decryptedToken = _secretProtector.Decrypt(account.TokenEncrypted);
        long followerCount;
        try
        {
            followerCount = await _instagramProvider.GetFollowerCountAsync(decryptedToken, account.InstagramUserId, cancellationToken);
        }
        catch (DomainException ex) when (ex.Message.Contains("INSTAGRAM_REAUTH_REQUIRED"))
        {
            account.MarkReauthorizationRequired("190_TOKEN_INVALID", now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        var changed = account.TryUpdateFollowers(followerCount, now);
        if (changed)
        {
            var history = new FollowerHistory
            {
                Id = Guid.NewGuid(),
                InstagramAccountId = account.Id,
                OldCount = account.PreviousFollowerCount ?? followerCount,
                NewCount = followerCount,
                ChangedAt = now
            };
            _dbContext.FollowerHistories.Add(history);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshFollowerResultDto(
            FollowerCount: account.FollowerCount ?? followerCount,
            FollowerSequence: account.FollowerSequence,
            Changed: changed,
            LastFollowerRefreshAt: account.LastFollowerRefreshAt ?? now
        );
    }

    public async Task DisconnectAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.InstagramAccounts
            .Include(a => a.DeviceBindings)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerUserId == userId, cancellationToken);

        if (account == null)
        {
            throw new NotFoundException(nameof(InstagramAccount), accountId);
        }

        if (!string.IsNullOrWhiteSpace(account.TokenEncrypted))
        {
            try
            {
                var decryptedToken = _secretProtector.Decrypt(account.TokenEncrypted);
                await _instagramProvider.RevokeAuthorizationAsync(decryptedToken, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke token at Meta during disconnect.");
            }
        }

        account.TokenEncrypted = string.Empty;
        account.RefreshTokenEncrypted = null;
        account.ConnectionStatus = InstagramConnectionStatus.Disconnected;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        // Deactivate bindings
        foreach (var binding in account.DeviceBindings.Where(b => b.Active))
        {
            binding.Active = false;
            binding.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync("InstagramDisconnected", "Success", userId, metadata: new { account.InstagramUserId }, cancellationToken: cancellationToken);
    }

    private static InstagramAccountDto MapToDto(InstagramAccount account)
    {
        return new InstagramAccountDto(
            Id: account.Id,
            InstagramUserId: account.InstagramUserId,
            Username: account.Username,
            AccountType: account.AccountType,
            ConnectionStatus: account.ConnectionStatus,
            RequiresReauthorization: account.RequiresReauthorization,
            FollowerCount: account.FollowerCount,
            PreviousFollowerCount: account.PreviousFollowerCount,
            FollowerSequence: account.FollowerSequence,
            LastFollowerRefreshAt: account.LastFollowerRefreshAt,
            TokenExpiresAt: account.TokenExpiresAt,
            CreatedAt: account.CreatedAt
        );
    }
}
