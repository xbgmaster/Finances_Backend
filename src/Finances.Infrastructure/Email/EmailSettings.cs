namespace Finances.Infrastructure.Email;

public class EmailSettings
{
    /// <summary>Delivery provider: "smtp" (default) or "brevo" (HTTP API, works on Render free tier).</summary>
    public string Provider { get; set; } = "smtp";

    /// <summary>API key for HTTP providers (e.g. Brevo).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "no-reply@finances.local";
    public string FromName { get; set; } = "Finances";
    public bool UseSsl { get; set; } = true;

    /// <summary>Send timeout in seconds. Keeps requests from hanging on a bad SMTP host.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    public bool UsesBrevo => string.Equals(Provider, "brevo", StringComparison.OrdinalIgnoreCase);
    public bool IsConfigured => UsesBrevo ? !string.IsNullOrWhiteSpace(ApiKey) : !string.IsNullOrWhiteSpace(Host);
}
