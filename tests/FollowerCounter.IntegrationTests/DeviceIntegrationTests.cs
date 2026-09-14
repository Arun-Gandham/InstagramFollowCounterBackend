using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using FluentAssertions;
using FollowerCounter.Application.DTOs.Admin;
using FollowerCounter.Application.DTOs.Auth;
using FollowerCounter.Application.DTOs.Device;
using FollowerCounter.Application.DTOs.Instagram;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Infrastructure.Development;
using FollowerCounter.IntegrationTests.Fixtures;

namespace FollowerCounter.IntegrationTests;

public class DeviceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DeviceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedCustomerAsync()
    {
        var client = _factory.CreateClientWithCookies();
        var email = $"hardware_user_{Guid.NewGuid():N}@counter.local";
        var password = "DeviceUserPassword123!";

        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Hardware Tester"));
        var regDto = await reg.Content.ReadFromJsonAsync<RegisterResponseDto>();

        var token = _factory.EmailService.SentVerifications.First(e => e.Email == email).Token;
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequestDto(regDto!.Id, token));

        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        return (client, regDto.Id);
    }

    private async Task<HttpClient> LoginAsAdminAsync()
    {
        var adminClient = _factory.CreateClientWithCookies();
        var loginResponse = await adminClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto("admin@counter.local", "AdminPass123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return adminClient;
    }

    [Fact]
    public async Task CompleteDeviceFlow_Provision_Claim_BindInstagram_PollState_AndIncrementSequence()
    {
        var adminClient = await LoginAsAdminAsync();
        var (customerClient, customerId) = await CreateAuthenticatedCustomerAsync();

        // 1. Admin provisions new hardware device
        var serial = $"FC-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var createDeviceResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/devices", new CreateDeviceRequestDto(serial));
        createDeviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var deviceProvisioned = await createDeviceResponse.Content.ReadFromJsonAsync<CreateDeviceResponseDto>();
        Assert.NotNull(deviceProvisioned);
        deviceProvisioned!.SerialNumber.Should().Be(serial);

        // 2. Customer claims device using serial number and packaging claim code
        var claimResponse = await customerClient.PostAsJsonAsync("/api/v1/devices/claim", new ClaimDeviceRequestDto(serial, deviceProvisioned.PlaintextClaimCode));
        claimResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Double claim race test: A second attempt to claim must return Conflict (409)
        var secondClaim = await customerClient.PostAsJsonAsync("/api/v1/devices/claim", new ClaimDeviceRequestDto(serial, deviceProvisioned.PlaintextClaimCode));
        secondClaim.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 4. Customer connects Instagram account
        var connect = await customerClient.GetAsync("/api/v1/instagram/connect");
        var connectDto = await connect.Content.ReadFromJsonAsync<InstagramConnectResponseDto>();
        var state = HttpUtility.ParseQueryString(connectDto!.AuthorizationUrl.Split('?')[1])["state"];
        await customerClient.GetAsync($"/api/v1/instagram/callback?code=mock_code&state={state}");

        var userAccounts = await (await customerClient.GetAsync("/api/v1/instagram/accounts")).Content.ReadFromJsonAsync<List<InstagramAccountDto>>();
        var igAccountId = userAccounts!.First().Id;

        // 5. Customer links device to Instagram account
        var bindResponse = await customerClient.PostAsync($"/api/v1/devices/{deviceProvisioned.DeviceId}/instagram/{igAccountId}", null);
        bindResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Hardware Counter calls GET /device/v1/state using device manufacturing credentials
        var deviceHttpClient = _factory.CreateClient();
        deviceHttpClient.DefaultRequestHeaders.Add("X-Device-Serial", serial);
        deviceHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Device", deviceProvisioned.PlaintextDeviceSecret);

        var stateResponse = await deviceHttpClient.GetAsync("/device/v1/state");
        stateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deviceState = await stateResponse.Content.ReadFromJsonAsync<DeviceStateResponseDto>();
        Assert.NotNull(deviceState);
        deviceState!.DeviceId.Should().Be(serial);
        deviceState.Configured.Should().BeTrue();
        deviceState.InstagramConnected.Should().BeTrue();
        deviceState.Followers.Should().Be(18920);
        deviceState.Sequence.Should().Be(0);
        deviceState.IsStale.Should().BeFalse();

        // 7. Follower Count Changes: 18920 -> 18921
        FakeInstagramProvider.SetFollowerCount(18921);

        // Trigger refresh
        var refreshResult = await customerClient.PostAsync($"/api/v1/instagram/accounts/{igAccountId}/refresh", null);
        refreshResult.StatusCode.Should().Be(HttpStatusCode.OK);

        // Counter polls /device/v1/state again
        var updatedStateResponse = await deviceHttpClient.GetAsync("/device/v1/state");
        var updatedState = await updatedStateResponse.Content.ReadFromJsonAsync<DeviceStateResponseDto>();

        updatedState!.Followers.Should().Be(18921);
        updatedState.Sequence.Should().Be(1, "Sequence must atomically increment when follower count changes!");

        // 8. Stale value retention: simulate Meta outage
        FakeInstagramProvider.SetSimulateOutage(true);
        try
        {
            // Even though Meta is down, polling /device/v1/state still returns last known count (18921)
            var outageStateResponse = await deviceHttpClient.GetAsync("/device/v1/state");
            outageStateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var outageState = await outageStateResponse.Content.ReadFromJsonAsync<DeviceStateResponseDto>();
            outageState!.Followers.Should().Be(18921, "Follower count must NEVER be zeroed during external outages!");
        }
        finally
        {
            FakeInstagramProvider.SetSimulateOutage(false);
        }

        // 9. Telemetry Heartbeat
        var heartbeatResponse = await deviceHttpClient.PostAsJsonAsync("/device/v1/heartbeat", new DeviceHeartbeatRequestDto(
            FirmwareVersion: "1.0.4",
            UptimeSeconds: 120500,
            WifiRssi: -58,
            FreeHeap: 85200,
            LastSequence: 1,
            Status: "OK"
        ));
        heartbeatResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeviceAuthentication_RejectsWrongSecret_AndMissingHeaders()
    {
        var deviceHttpClient = _factory.CreateClient();

        // 1. Missing credentials entirely
        var response1 = await deviceHttpClient.GetAsync("/device/v1/state");
        response1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Wrong device secret
        deviceHttpClient.DefaultRequestHeaders.Add("X-Device-Serial", "FC-A82F32");
        deviceHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Device", "wrong_invalid_secret_value");

        var response2 = await deviceHttpClient.GetAsync("/device/v1/state");
        response2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
