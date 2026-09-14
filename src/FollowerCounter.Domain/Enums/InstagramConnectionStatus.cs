using System.Text.Json.Serialization;

namespace FollowerCounter.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstagramConnectionStatus
{
    Connected = 1,
    TokenExpiring = 2,
    Refreshing = 3,
    Expired = 4,
    ReauthorizationRequired = 5,
    RateLimited = 6,
    TemporarilyUnavailable = 7,
    Disconnected = 8,
    Error = 9
}
