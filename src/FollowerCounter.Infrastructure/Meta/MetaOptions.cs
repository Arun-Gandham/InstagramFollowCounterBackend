namespace FollowerCounter.Infrastructure.Meta;

public class MetaOptions
{
    public const string SectionName = "Meta";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "v22.0";
    public string AuthorizationUrl { get; set; } = "https://www.instagram.com/oauth/authorize";
    public string TokenUrl { get; set; } = "https://api.instagram.com/oauth/access_token";
    public string GraphBaseUrl { get; set; } = "https://graph.instagram.com";
}
