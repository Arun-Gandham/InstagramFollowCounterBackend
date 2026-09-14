using System.Text.Json.Serialization;

namespace FollowerCounter.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserStatus
{
    Active = 1,
    Suspended = 2,
    Deleted = 3
}
