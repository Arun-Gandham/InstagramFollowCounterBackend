using System.Net.Http.Headers;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Infrastructure.Development;
using FollowerCounter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FollowerCounter.IntegrationTests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestEmailService EmailService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace email service with test spy
            services.AddSingleton<IEmailService>(EmailService);

            // Ensure FakeInstagramProvider is active for offline testing
            services.AddSingleton<IInstagramProvider, FakeInstagramProvider>();
        });
    }

    public HttpClient CreateClientWithCookies()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }
}

public class TestEmailService : IEmailService
{
    public List<(string Email, string Token, string Link)> SentVerifications { get; } = new();
    public List<(string Email, string Link)> SentResets { get; } = new();

    public Task SendEmailVerificationAsync(string recipientEmail, string recipientName, string verificationLink, CancellationToken cancellationToken = default)
    {
        // Extract token from link: ...&token=...
        var uri = new Uri(verificationLink);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var token = query["token"] ?? string.Empty;

        SentVerifications.Add((recipientEmail, token, verificationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string recipientEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default)
    {
        SentResets.Add((recipientEmail, resetLink));
        return Task.CompletedTask;
    }

    public Task SendSecurityAlertAsync(string recipientEmail, string recipientName, string alertMessage, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
