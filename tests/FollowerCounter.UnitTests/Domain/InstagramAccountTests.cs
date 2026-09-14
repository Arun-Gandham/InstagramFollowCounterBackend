using FluentAssertions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;

namespace FollowerCounter.UnitTests.Domain;

public class InstagramAccountTests
{
    [Fact]
    public void TryUpdateFollowers_WhenCountChanges_ShouldIncrementSequenceAndStorePrevious()
    {
        // Arrange
        var account = new InstagramAccount
        {
            FollowerCount = 18920,
            PreviousFollowerCount = 18919,
            FollowerSequence = 103
        };
        var now = DateTimeOffset.UtcNow;

        // Act
        var changed = account.TryUpdateFollowers(18921, now);

        // Assert
        changed.Should().BeTrue();
        account.PreviousFollowerCount.Should().Be(18920);
        account.FollowerCount.Should().Be(18921);
        account.FollowerSequence.Should().Be(104);
        account.LastFollowerRefreshAt.Should().Be(now);
        account.ConnectionStatus.Should().Be(InstagramConnectionStatus.Connected);
        account.RequiresReauthorization.Should().BeFalse();
    }

    [Fact]
    public void TryUpdateFollowers_WhenCountUnchanged_ShouldNotIncrementSequence()
    {
        // Arrange
        var account = new InstagramAccount
        {
            FollowerCount = 18921,
            PreviousFollowerCount = 18920,
            FollowerSequence = 104
        };
        var now = DateTimeOffset.UtcNow;

        // Act
        var changed = account.TryUpdateFollowers(18921, now);

        // Assert
        changed.Should().BeFalse();
        account.FollowerCount.Should().Be(18921);
        account.PreviousFollowerCount.Should().Be(18920);
        account.FollowerSequence.Should().Be(104); // must NOT increment!
        account.LastFollowerRefreshAt.Should().Be(now);
    }

    [Fact]
    public void MarkReauthorizationRequired_MustNeverResetFollowerCount()
    {
        // Arrange
        var account = new InstagramAccount
        {
            FollowerCount = 18921,
            FollowerSequence = 104
        };
        var now = DateTimeOffset.UtcNow;

        // Act
        account.MarkReauthorizationRequired("190_TOKEN_REVOKED", now);

        // Assert
        account.FollowerCount.Should().Be(18921, "Follower count must NEVER be erased or zeroed during auth failures!");
        account.ConnectionStatus.Should().Be(InstagramConnectionStatus.ReauthorizationRequired);
        account.RequiresReauthorization.Should().BeTrue();
        account.LastApiErrorCode.Should().Be("190_TOKEN_REVOKED");
    }
}
