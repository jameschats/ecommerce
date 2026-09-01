namespace ecomm.api.Features.MarketingStudio;

/// <summary>Static, public OAuth endpoints + default scopes for each supported network. These URLs are
/// stable and app-independent; the per-app client id/secret come from <see cref="SocialOptions"/>.</summary>
public sealed record SocialPlatformInfo(
    string Key, string DisplayName, string AuthorizeUrl, string TokenUrl,
    string DefaultScopes, string ScopeSeparator = " ");

public static class SocialPlatforms
{
    public static readonly IReadOnlyList<SocialPlatformInfo> All = new[]
    {
        new SocialPlatformInfo("facebook", "Facebook",
            "https://www.facebook.com/v19.0/dialog/oauth",
            "https://graph.facebook.com/v19.0/oauth/access_token",
            "pages_manage_posts,pages_read_engagement,business_management", ","),
        new SocialPlatformInfo("instagram", "Instagram",
            "https://www.facebook.com/v19.0/dialog/oauth",
            "https://graph.facebook.com/v19.0/oauth/access_token",
            "instagram_basic,instagram_content_publish,pages_show_list,business_management", ","),
        new SocialPlatformInfo("linkedin", "LinkedIn",
            "https://www.linkedin.com/oauth/v2/authorization",
            "https://www.linkedin.com/oauth/v2/accessToken",
            "w_member_social openid profile"),
        new SocialPlatformInfo("pinterest", "Pinterest",
            "https://www.pinterest.com/oauth/",
            "https://api.pinterest.com/v5/oauth/token",
            "boards:read,pins:read,pins:write", ","),
        new SocialPlatformInfo("youtube", "YouTube",
            "https://accounts.google.com/o/oauth2/v2/auth",
            "https://oauth2.googleapis.com/token",
            "https://www.googleapis.com/auth/youtube.upload"),
        new SocialPlatformInfo("googleads", "Google Ads",
            "https://accounts.google.com/o/oauth2/v2/auth",
            "https://oauth2.googleapis.com/token",
            "https://www.googleapis.com/auth/adwords"),
        new SocialPlatformInfo("whatsapp", "WhatsApp",
            "", "", ""),   // WhatsApp uses BSP embedded-signup, not this OAuth flow (see §3.11) — shown as a card, connected via BSP.
    };

    public static SocialPlatformInfo? Get(string key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnown(string key) => Get(key) is not null;
}

/// <summary>
/// Config section "Social" — the platform apps WavCommerce registers centrally. Client id/secret per
/// network + the OAuth redirect base. Empty client id → that platform shows as "not configured yet"
/// (Connect disabled) until keys are added to user-secrets/env. Never commit real secrets.
/// </summary>
public sealed class SocialOptions
{
    public const string SectionName = "Social";

    /// <summary>Base URL the OAuth providers redirect back to, e.g. "https://app.wavcommerce.online".
    /// The full callback is <c>{RedirectBaseUrl}/api/marketing/connections/{platform}/callback</c> and
    /// must be registered in each platform app. One central host across all tenants; the tenant is
    /// carried in the signed <c>state</c>, not the host.</summary>
    public string RedirectBaseUrl { get; set; } = "";

    public Dictionary<string, SocialProviderConfig> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SocialProviderConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}
