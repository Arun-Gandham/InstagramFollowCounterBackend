using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FollowerCounter.Application.DTOs.Auth;
using FollowerCounter.IntegrationTests.Fixtures;

namespace FollowerCounter.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompleteUserLifecycle_Register_Verify_Login_Me_ChangePassword_Logout()
    {
        var client = _factory.CreateClientWithCookies();
        var uniqueEmail = $"user_{Guid.NewGuid():N}@example.com";
        var password = "InitialPassword123!";
        var displayName = "Test Creator";

        // 1. Register
        var registerRequest = new RegisterRequestDto(uniqueEmail, password, displayName);
        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var regResult = await regResponse.Content.ReadFromJsonAsync<RegisterResponseDto>();
        Assert.NotNull(regResult);
        regResult!.Email.Should().Be(uniqueEmail);

        // 2. Extract verification email token
        var emailEntry = _factory.EmailService.SentVerifications.FirstOrDefault(e => e.Email == uniqueEmail);
        emailEntry.Token.Should().NotBeNullOrWhiteSpace();

        // 3. Attempt login before email confirmation -> Should fail
        var loginBeforeVerify = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(uniqueEmail, password));
        loginBeforeVerify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Verify Email
        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequestDto(regResult.Id, emailEntry.Token));
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Login -> Succeeded with HttpOnly cookie
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(uniqueEmail, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        loginResult!.Email.Should().Be(uniqueEmail);
        loginResult.Roles.Should().Contain("Customer");

        // 6. Query /api/v1/auth/me using authenticated cookie session
        var meResponse = await client.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResult = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        meResult!.Email.Should().Be(uniqueEmail);
        meResult.DisplayName.Should().Be(displayName);

        // 7. Change Password
        var newPassword = "UpdatedPassword123!";
        var changePassResponse = await client.PostAsJsonAsync("/api/v1/auth/change-password", new ChangePasswordRequestDto(password, newPassword));
        changePassResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 8. Logout
        var logoutResponse = await client.PostAsync("/api/v1/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 9. Login with new password
        var newLoginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(uniqueEmail, newPassword));
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_ShouldNotRevealIfEmailExists()
    {
        var client = _factory.CreateClientWithCookies();

        // Request for non-existent email
        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequestDto("nonexistent_user@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("If an account exists for this email, password reset instructions have been sent.");
    }

    [Fact]
    public async Task SeededUsers_CanAuthenticateWithDesignatedRolesAndPermissions()
    {
        // 1. Super Admin authentication
        var superAdminClient = _factory.CreateClientWithCookies();
        var superLogin = await superAdminClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(
            "arunsaigandham1998@gmail.com",
            "G_arunsai@1998"
        ));
        superLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var superUser = await superLogin.Content.ReadFromJsonAsync<LoginResponseDto>();
        superUser!.Roles.Should().Contain("SuperAdmin");
        superUser.Roles.Should().Contain("Admin");

        // Super Admin can access admin APIs
        var adminEndpoint = await superAdminClient.GetAsync("/api/v1/admin/users");
        adminEndpoint.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Support user authentication
        var supportClient = _factory.CreateClientWithCookies();
        var supportLogin = await supportClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(
            "support@counter.local",
            "SupportPass123!"
        ));
        supportLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var supportUser = await supportLogin.Content.ReadFromJsonAsync<LoginResponseDto>();
        supportUser!.Roles.Should().Contain("Support");

        // 3. Customer user authentication
        var customerClient = _factory.CreateClientWithCookies();
        var customerLogin = await customerClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(
            "customer@counter.local",
            "CustomerPass123!"
        ));
        customerLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var customerUser = await customerLogin.Content.ReadFromJsonAsync<LoginResponseDto>();
        customerUser!.Roles.Should().Contain("Customer");
    }
}
