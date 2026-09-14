using FluentAssertions;
using FollowerCounter.Infrastructure.Security;

namespace FollowerCounter.UnitTests.Security;

public class CryptoHelperTests
{
    [Fact]
    public void GenerateSecureToken_ShouldProduceDistinctHexStrings()
    {
        var token1 = CryptoHelper.GenerateSecureToken(32);
        var token2 = CryptoHelper.GenerateSecureToken(32);

        token1.Should().NotBeNullOrWhiteSpace();
        token2.Should().NotBeNullOrWhiteSpace();
        token1.Length.Should().Be(64); // 32 bytes = 64 hex characters
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateClaimCode_ShouldMatchFormat()
    {
        var claimCode = CryptoHelper.GenerateClaimCode();

        claimCode.Should().StartWith("CLM-");
        claimCode.Split('-').Length.Should().Be(4); // CLM-XXX-XXX-XXX
    }

    [Fact]
    public void ComputeSha256Hash_ShouldBeDeterministic()
    {
        var input = "TestDeviceSecret123!";
        var hash1 = CryptoHelper.ComputeSha256Hash(input);
        var hash2 = CryptoHelper.ComputeSha256Hash(input);

        hash1.Should().Be(hash2);
        hash1.Length.Should().Be(64);
    }

    [Fact]
    public void FixedTimeEquals_ShouldMatchAccurately()
    {
        var s1 = "SuperSecretDeviceSecretString";
        var s2 = "SuperSecretDeviceSecretString";
        var s3 = "DifferentSecretDeviceString";

        CryptoHelper.FixedTimeEquals(s1, s2).Should().BeTrue();
        CryptoHelper.FixedTimeEquals(s1, s3).Should().BeFalse();
    }
}
