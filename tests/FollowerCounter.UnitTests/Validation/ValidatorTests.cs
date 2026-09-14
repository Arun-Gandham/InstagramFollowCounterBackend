using FluentAssertions;
using FollowerCounter.Application.DTOs.Auth;
using FollowerCounter.Application.DTOs.Device;
using FollowerCounter.Application.Validators;

namespace FollowerCounter.UnitTests.Validation;

public class ValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly ClaimDeviceRequestValidator _claimValidator = new();

    [Theory]
    [InlineData("short", false)]                         // Too short (<10)
    [InlineData("alllowercase123!", false)]              // Missing uppercase
    [InlineData("ALLUPPERCASE123!", false)]              // Missing lowercase
    [InlineData("NoDigitsInPassword!", false)]           // Missing digit
    [InlineData("NoSpecialCharacters123", false)]        // Missing special character
    [InlineData("ValidP@ssword1234", true)]              // Valid
    public void RegisterValidator_PasswordComplexity_ShouldBeEnforced(string password, bool shouldBeValid)
    {
        var request = new RegisterRequestDto("test@example.com", password, "Test User");
        var result = _registerValidator.Validate(request);

        result.IsValid.Should().Be(shouldBeValid);
    }

    [Theory]
    [InlineData("", "CLM-123", false)]
    [InlineData("FC-123", "", false)]
    [InlineData("FC-123", "CLM-1234-5678", true)]
    public void ClaimValidator_InputRules_ShouldBeEnforced(string serial, string code, bool shouldBeValid)
    {
        var request = new ClaimDeviceRequestDto(serial, code);
        var result = _claimValidator.Validate(request);

        result.IsValid.Should().Be(shouldBeValid);
    }
}
