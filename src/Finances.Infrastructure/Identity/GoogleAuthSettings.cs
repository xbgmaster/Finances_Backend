namespace Finances.Infrastructure.Identity;

/// <summary>
/// Web OAuth client id used as the audience of Google ID tokens.
/// Android must request the token with this same web client id.
/// </summary>
public class GoogleAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
