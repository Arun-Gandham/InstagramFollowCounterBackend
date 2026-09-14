namespace FollowerCounter.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string recipientEmail, string recipientName, string verificationLink, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string recipientEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default);

    Task SendSecurityAlertAsync(string recipientEmail, string recipientName, string alertMessage, CancellationToken cancellationToken = default);
}
