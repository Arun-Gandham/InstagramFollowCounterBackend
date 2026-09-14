namespace FollowerCounter.Application.DTOs.Instagram;

public record InstagramProfile(
    string Id,
    string Username,
    string? Name,
    string? AccountType,
    long? FollowersCount
);
