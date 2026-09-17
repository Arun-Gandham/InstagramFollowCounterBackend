using FluentValidation;
using FollowerCounter.Application.DTOs.Admin;
using FollowerCounter.Application.DTOs.Auth;
using FollowerCounter.Application.DTOs.Device;

namespace FollowerCounter.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Must(p => !string.IsNullOrEmpty(p) && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(100).WithMessage("Display name must not exceed 100 characters.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class ClaimDeviceRequestValidator : AbstractValidator<ClaimDeviceRequestDto>
{
    public ClaimDeviceRequestValidator()
    {
        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("Device serial number is required.")
            .MaximumLength(50).WithMessage("Serial number must not exceed 50 characters.");

        RuleFor(x => x.ClaimCode)
            .NotEmpty().WithMessage("Claim code is required.")
            .MaximumLength(100).WithMessage("Claim code must not exceed 100 characters.");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Must(p => !string.IsNullOrEmpty(p) && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage("Password must contain at least one special character.");
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Must(p => !string.IsNullOrEmpty(p) && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage("Password must contain at least one special character.");
    }
}

public class CreateDeviceRequestValidator : AbstractValidator<CreateDeviceRequestDto>
{
    public CreateDeviceRequestValidator()
    {
        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("Serial number is required.")
            .Matches(@"^[A-Za-z0-9\-_]{4,50}$").WithMessage("Serial number must be alphanumeric between 4 and 50 characters.");

        RuleFor(x => x.DigitCount)
            .Must(d => d == 5 || d == 7)
            .WithMessage("Digit count must be either 5 or 7.");
    }
}

public class UpdateDeviceRequestValidator : AbstractValidator<UpdateDeviceRequestDto>
{
    public UpdateDeviceRequestValidator()
    {
        RuleFor(x => x.DigitCount)
            .Must(d => !d.HasValue || d.Value == 5 || d.Value == 7)
            .WithMessage("Digit count must be either 5 or 7.");

        RuleFor(x => x.SerialNumber)
            .Matches(@"^[A-Za-z0-9\-_]{4,50}$")
            .When(x => !string.IsNullOrEmpty(x.SerialNumber))
            .WithMessage("Serial number must be alphanumeric between 4 and 50 characters.");
    }
}

