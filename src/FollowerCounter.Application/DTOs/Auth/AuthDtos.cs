namespace FollowerCounter.Application.DTOs.Auth;

public record RegisterRequestDto(
    string Email,
    string Password,
    string DisplayName
);

public record RegisterResponseDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Message
);

public record LoginRequestDto(
    string Email,
    string Password
);

public record LoginResponseDto(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles
);

public record VerifyEmailRequestDto(
    Guid UserId,
    string Token
);

public record ForgotPasswordRequestDto(
    string Email
);

public record ResetPasswordRequestDto(
    Guid UserId,
    string Token,
    string NewPassword
);

public record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword
);

public record DeleteAccountRequestDto(
    string Password
);

public record CurrentUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt
);
