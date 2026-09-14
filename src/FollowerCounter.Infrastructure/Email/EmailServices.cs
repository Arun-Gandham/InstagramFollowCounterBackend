using FollowerCounter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace FollowerCounter.Infrastructure.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Console"; // "Console" or "Smtp"
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = "no-reply@counter.example.com";
    public string SenderName { get; set; } = "Instagram Counter Support";
}

public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(string recipientEmail, string recipientName, string verificationLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("================== [DEVELOPMENT EMAIL] ==================\n" +
                               "To: {Email} ({Name})\n" +
                               "Subject: Verify your Instagram Counter account\n" +
                               "Verification Link: {Link}\n" +
                               "=========================================================",
                               recipientEmail, recipientName, verificationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string recipientEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("================== [DEVELOPMENT EMAIL] ==================\n" +
                               "To: {Email} ({Name})\n" +
                               "Subject: Reset your Instagram Counter password\n" +
                               "Reset Link: {Link}\n" +
                               "=========================================================",
                               recipientEmail, recipientName, resetLink);
        return Task.CompletedTask;
    }

    public Task SendSecurityAlertAsync(string recipientEmail, string recipientName, string alertMessage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("================== [DEVELOPMENT EMAIL] ==================\n" +
                               "To: {Email} ({Name})\n" +
                               "Subject: Security Alert\n" +
                               "Message: {Message}\n" +
                               "=========================================================",
                               recipientEmail, recipientName, alertMessage);
        return Task.CompletedTask;
    }
}

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailVerificationAsync(string recipientEmail, string recipientName, string verificationLink, CancellationToken cancellationToken = default)
    {
        var subject = "Verify your Instagram Counter Account";
        var body = $"Hello {recipientName},\n\nPlease verify your email by clicking the link below:\n{verificationLink}\n\nThank you!";
        await SendMailAsync(recipientEmail, recipientName, subject, body);
    }

    public async Task SendPasswordResetAsync(string recipientEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default)
    {
        var subject = "Reset your Instagram Counter Password";
        var body = $"Hello {recipientName},\n\nYou requested a password reset. Click the link below to set a new password:\n{resetLink}\n\nIf you did not request this, please ignore this email.";
        await SendMailAsync(recipientEmail, recipientName, subject, body);
    }

    public async Task SendSecurityAlertAsync(string recipientEmail, string recipientName, string alertMessage, CancellationToken cancellationToken = default)
    {
        var subject = "Security Notification: Instagram Counter Account";
        var body = $"Hello {recipientName},\n\n{alertMessage}\n\nIf this was not you, please secure your account immediately.";
        await SendMailAsync(recipientEmail, recipientName, subject, body);
    }

    private async Task SendMailAsync(string recipientEmail, string recipientName, string subject, string body)
    {
        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_options.SenderEmail, _options.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(recipientEmail, recipientName));

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {RecipientEmail}", recipientEmail);
            throw;
        }
    }
}
