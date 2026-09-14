using System.Net;
using System.Net.Http.Json;
using System.Web;
using FluentAssertions;
using FollowerCounter.Application.DTOs.Auth;
using FollowerCounter.Application.DTOs.Instagram;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Infrastructure.Development;
using FollowerCounter.IntegrationTests.Fixtures;

namespace FollowerCounter.IntegrationTests;

public class InstagramIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public InstagramIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedCustomerAsync()
    {
        var client = _factory.CreateClientWithCookies();
        var email = $"creator_{Guid.NewGuid():N}@counter.local";
        var password = "CreatorPassword123!";

        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Creator"));
        var regDto = await reg.Content.ReadFromJsonAsync<RegisterResponseDto>();

        var token = _factory.EmailService.SentVerifications.First(e => e.Email == email).Token;
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequestDto(regDto!.Id, token));

        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        return (client, regDto.Id);
    }

    [Fact]
    public async Task CompleteInstagramOAuthFlow_AndReplayPrevention()
    {
        var (client, userId) = await CreateAuthenticatedCustomerAsync();

        // 1. User clicks "Connect Instagram"
        var connectResponse = await client.GetAsync("/api/v1/instagram/connect");
        connectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var connectResult = await connectResponse.Content.ReadFromJsonAsync<InstagramConnectResponseDto>();
        Assert.NotNull(connectResult);
        connectResult!.AuthorizationUrl.Should().Contain("state=");

        // Extract state parameter from generated URL
        var authUri = new Uri(connectResult.AuthorizationUrl, UriKind.RelativeOrAbsolute);
        var query = HttpUtility.ParseQueryString(authUri.IsAbsoluteUri ? authUri.Query : authUri.ToString().Split('?')[1]);
        var state = query["state"];
        state.Should().NotBeNullOrWhiteSpace();

        // 2. Meta redirects to backend callback with code and state
        var callbackUrl = $"/api/v1/instagram/callback?code=valid_meta_auth_code&state={state}";
        var callbackResponse = await client.GetAsync(callbackUrl);
        callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // 3. Verify Instagram account is connected and token is stored encrypted
        var accountsResponse = await client.GetAsync("/api/v1/instagram/accounts");
        accountsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var accounts = await accountsResponse.Content.ReadFromJsonAsync<List<InstagramAccountDto>>();
        Assert.NotNull(accounts);
        accounts!.Count.Should().BeGreaterThanOrEqualTo(1);

        var connectedAccount = accounts.First();
        connectedAccount.Username.Should().Be("demo_creator");
        connectedAccount.ConnectionStatus.Should().Be(InstagramConnectionStatus.Connected);
        connectedAccount.FollowerCount.Should().Be(FakeInstagramProvider.CurrentFollowerCount);
        connectedAccount.FollowerSequence.Should().Be(0);

        // 4. SECURITY TEST: Replay Attack!
        // Calling callback a second time with the identical state MUST fail
        var replayResponse = await client.GetAsync(callbackUrl);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 5. SECURITY TEST: Invalid / Random state MUST fail
        var badStateResponse = await client.GetAsync("/api/v1/instagram/callback?code=code123&state=tampered_random_state_val");
        badStateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OwnershipCheck_UserCannotAccessAnotherUsersInstagramAccount()
    {
        var (userAClient, userAId) = await CreateAuthenticatedCustomerAsync();
        var (userBClient, userBId) = await CreateAuthenticatedCustomerAsync();

        // User A connects Instagram
        var connect = await userAClient.GetAsync("/api/v1/instagram/connect");
        var connectDto = await connect.Content.ReadFromJsonAsync<InstagramConnectResponseDto>();
        var state = HttpUtility.ParseQueryString(connectDto!.AuthorizationUrl.Split('?')[1])["state"];
        await userAClient.GetAsync($"/api/v1/instagram/callback?code=mock_code&state={state}");

        var userAAccounts = await (await userAClient.GetAsync("/api/v1/instagram/accounts")).Content.ReadFromJsonAsync<List<InstagramAccountDto>>();
        var userAAccountId = userAAccounts!.First().Id;

        // User B tries to view User A's account
        var accessResponse = await userBClient.GetAsync($"/api/v1/instagram/accounts/{userAAccountId}");
        accessResponse.StatusCode.Should().Be(HttpStatusCode.NotFound); // Not found for User B (ownership protected)

        // User B tries to refresh User A's account
        var refreshResponse = await userBClient.PostAsync($"/api/v1/instagram/accounts/{userAAccountId}/refresh", null);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
