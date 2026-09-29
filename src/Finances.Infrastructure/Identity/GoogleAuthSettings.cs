namespace Finances.Infrastructure.Identity;

/// <summary>
/// Web OAuth client id used as the audience of Google ID tokens.
/// Android must request the token with this same web client id.
/// </summary>
public class GoogleAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string AndroidClientId { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    public IReadOnlyList<string> Audiences =>
        new[] { ClientId, AndroidClientId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
