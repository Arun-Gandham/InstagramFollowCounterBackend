using FluentAssertions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Domain.Exceptions;

namespace FollowerCounter.UnitTests.Domain;

public class DeviceTests
{
    [Fact]
    public void Claim_UnclaimedDevice_ShouldSetOwnerAndActiveStatus()
    {
        // Arrange
        var device = new Device
        {
            SerialNumber = "FC-A82F32",
            Status = DeviceStatus.Unclaimed
        };
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        device.Claim(userId, now);

        // Assert
        device.OwnerUserId.Should().Be(userId);
        device.Status.Should().Be(DeviceStatus.Active);
        device.ClaimedAt.Should().Be(now);
    }

    [Fact]
    public void Claim_AlreadyClaimedDevice_ShouldThrowDomainException()
    {
        // Arrange
        var device = new Device
        {
            SerialNumber = "FC-A82F32",
            Status = DeviceStatus.Active,
            OwnerUserId = Guid.NewGuid()
        };

        // Act
        var act = () => device.Claim(Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*already been claimed*");
    }

    [Fact]
    public void Claim_DisabledDevice_ShouldThrowDomainException()
    {
        // Arrange
        var device = new Device
        {
            SerialNumber = "FC-A82F32",
            Status = DeviceStatus.Disabled
        };

        // Act
        var act = () => device.Claim(Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be claimed*");
    }
}
