namespace Finances.Infrastructure;

/// <summary>Public URLs used to build links sent to users (e.g. password reset).</summary>
public class AppUrls
{
    /// <summary>Base URL of the SPA front-end (no trailing slash).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
