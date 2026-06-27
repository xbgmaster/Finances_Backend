using System.Net.Http.Json;
using Finances.Application.Common;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Email;

/// <summary>
/// Sends email through Brevo's transactional HTTP API (https://api.brevo.com).
/// Uses HTTPS (port 443), so it works on hosts that block outbound SMTP ports
/// (e.g. Render free tier). The 'From' address must be a verified Brevo sender.
/// </summary>
public class BrevoEmailSender : IEmailSender
{
    private const string Endpoint = "https://api.brevo.com/v3/smtp/email";

    private readonly EmailSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(
        EmailSettings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<BrevoEmailSender> logger)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning(
                "Brevo API key not configured. Email NOT sent to {To}. Subject: {Subject}\n{Body}",
                to, subject, htmlBody);
            return;
        }

        var payload = new
        {
            sender = new { name = _settings.FromName, email = _settings.From },
            to = new[] { new { email = to } },
            subject,
            htmlContent = htmlBody,
        };

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("api-key", _settings.ApiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Brevo API returned {(int)response.StatusCode}: {detail}");
        }

        _logger.LogInformation("Email sent to {To} via Brevo (subject: {Subject}).", to, subject);
    }
}
