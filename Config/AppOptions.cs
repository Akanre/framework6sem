namespace Pr3.ConfigAndSecurity.Config;

public sealed class AppOptions
{
    public AppMode Mode { get; set; } = AppMode.Учебный;

    public string[] TrustedOrigins { get; set; } = Array.Empty<string>();

    public RateLimitOptions RateLimits { get; set; } = new();
}
