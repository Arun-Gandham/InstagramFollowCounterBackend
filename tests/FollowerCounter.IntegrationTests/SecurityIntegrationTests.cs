using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FollowerCounter.Application.DTOs.Auth;
using FollowerCounter.IntegrationTests.Fixtures;

namespace FollowerCounter.IntegrationTests;

public class SecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomerRole_CannotAccessAdminEndpoints_Returns403Forbidden()
    {
        var client = _factory.CreateClientWithCookies();
        var email = $"regular_customer_{Guid.NewGuid():N}@counter.local";
        var password = "CustomerPassword123!";

        // Register and login as regular customer
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Regular Customer"));
        var regDto = await reg.Content.ReadFromJsonAsync<RegisterResponseDto>();
        var token = _factory.EmailService.SentVerifications.First(e => e.Email == email).Token;
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequestDto(regDto!.Id, token));
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));

        // Attempt admin endpoint access
        var adminUsers = await client.GetAsync("/api/v1/admin/users");
        adminUsers.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminDevices = await client.GetAsync("/api/v1/admin/devices");
        adminDevices.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminHealth = await client.GetAsync("/api/v1/admin/system-health");
        adminHealth.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HealthEndpoints_AreAccessibleAndReportStatus()
    {
        var client = _factory.CreateClient();

        var liveResponse = await client.GetAsync("/health/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var readyResponse = await client.GetAsync("/health/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Swagger_EndpointsAreAccessibleInDevelopment()
    {
        var client = _factory.CreateClient();

        var jsonResponse = await client.GetAsync("/swagger/v1/swagger.json");
        jsonResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await jsonResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Follower Counter API");
        content.Should().Contain("/device/v1/state");
        content.Should().Contain("/api/v1/instagram/connect");

        var uiResponse = await client.GetAsync("/swagger/index.html");
        uiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
