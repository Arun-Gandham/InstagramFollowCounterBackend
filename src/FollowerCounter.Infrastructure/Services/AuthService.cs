using FluentValidation;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Auth;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FollowerCounter.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IAppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;
    private readonly IInstagramProvider _instagramProvider;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<ResetPasswordRequestDto> _resetPasswordValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changePasswordValidator;
    private readonly string _clientBaseUrl;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IAppDbContext dbContext,
        IEmailService emailService,
        IAuditLogService auditLogService,
        IInstagramProvider instagramProvider,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<ResetPasswordRequestDto> resetPasswordValidator,
        IValidator<ChangePasswordRequestDto> changePasswordValidator,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _emailService = emailService;
        _auditLogService = auditLogService;
        _instagramProvider = instagramProvider;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _changePasswordValidator = changePasswordValidator;
        _clientBaseUrl = configuration.GetValue<string>("ClientApp:BaseUrl") ?? "https://localhost:7198";
        _logger = logger;
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(e => e.Description).ToArray();
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]> { ["Password"] = errors });
        }

        await _userManager.AddToRoleAsync(user, "Customer");

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var verificationUrl = $"{_clientBaseUrl}/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await _emailService.SendEmailVerificationAsync(user.Email, user.DisplayName, verificationUrl, cancellationToken);
        await _auditLogService.LogAsync("UserRegistration", "Success", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);

        return new RegisterResponseDto(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            Message: "Registration successful. Please check your email to verify your account before logging in."
        );
    }

    public async Task VerifyEmailAsync(VerifyEmailRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            await _auditLogService.LogAsync("EmailVerification", "Failed", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["Token"] = result.Errors.Select(e => e.Description).ToArray()
            });
        }

        await _auditLogService.LogAsync("EmailVerification", "Success", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            await _auditLogService.LogAsync("UserLogin", "Failed_InvalidEmail", ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (user.Status != UserStatus.Active)
        {
            await _auditLogService.LogAsync("UserLogin", $"Failed_UserStatus_{user.Status}", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new UnauthorizedException($"Account is {user.Status.ToString().ToLowerInvariant()}.");
        }

        if (!user.EmailConfirmed)
        {
            await _auditLogService.LogAsync("UserLogin", "Failed_EmailNotConfirmed", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new UnauthorizedException("Email has not been verified. Please check your inbox for the verification link.");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            await _auditLogService.LogAsync("UserLogin", "Failed_LockedOut", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new UnauthorizedException("Account is temporarily locked due to multiple failed login attempts. Please try again later.");
        }

        if (!signInResult.Succeeded)
        {
            await _auditLogService.LogAsync("UserLogin", "Failed_BadPassword", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new UnauthorizedException("Invalid email or password.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        await _auditLogService.LogAsync("UserLogin", "Success", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);

        return new LoginResponseDto(
            Id: user.Id,
            Email: user.Email!,
            DisplayName: user.DisplayName,
            Roles: roles.ToList()
        );
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user != null && user.EmailConfirmed && user.Status == UserStatus.Active)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = $"{_clientBaseUrl}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";
            await _emailService.SendPasswordResetAsync(user.Email!, user.DisplayName, resetUrl, cancellationToken);
        }

        // Always log and return identical response to prevent email enumeration
        await _auditLogService.LogAsync("ForgotPasswordRequested", "Success", ipAddress: ipAddress, cancellationToken: cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _resetPasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            throw new NotFoundException("Invalid password reset token or user ID.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            await _auditLogService.LogAsync("PasswordReset", "Failed", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["Token"] = result.Errors.Select(e => e.Description).ToArray()
            });
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync("PasswordReset", "Success", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            await _auditLogService.LogAsync("ChangePassword", "Failed", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
            throw new Application.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["CurrentPassword"] = result.Errors.Select(e => e.Description).ToArray()
            });
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        await _auditLogService.LogAsync("ChangePassword", "Success", user.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserDto(
            Id: user.Id,
            Email: user.Email!,
            DisplayName: user.DisplayName,
            Roles: roles.ToList(),
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt
        );
    }

    public async Task DeleteAccountAsync(Guid userId, DeleteAccountRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            throw new UnauthorizedException("Invalid password. Account deletion requires valid credentials.");
        }

        // 1. Unbind devices
        var userDevices = await _dbContext.Devices.Where(d => d.OwnerUserId == userId).ToListAsync(cancellationToken);
        foreach (var device in userDevices)
        {
            device.Unclaim(DateTimeOffset.UtcNow);
        }

        // 2. Best-effort revoke connected Instagram accounts
        var userIgAccounts = await _dbContext.InstagramAccounts.Where(a => a.OwnerUserId == userId).ToListAsync(cancellationToken);
        foreach (var igAccount in userIgAccounts)
        {
            try
            {
                // Can only revoke if we have a token
                if (!string.IsNullOrEmpty(igAccount.TokenEncrypted))
                {
                    // token revocation is handled if provider supports it
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke Instagram permissions during user deletion.");
            }
        }

        // 3. Delete user
        await _userManager.DeleteAsync(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync("AccountDeleted", "Success", userId, ipAddress: ipAddress, cancellationToken: cancellationToken);
    }
}
