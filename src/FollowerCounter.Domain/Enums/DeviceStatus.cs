using System.Text.Json.Serialization;

namespace FollowerCounter.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeviceStatus
{
    Manufactured = 1,
    Unclaimed = 2,
    Active = 3,
    Disabled = 4,
    Revoked = 5
}
