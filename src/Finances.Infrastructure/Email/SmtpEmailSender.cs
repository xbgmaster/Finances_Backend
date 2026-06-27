using System.Net;
using System.Net.Mail;
using Finances.Application.Common;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Email;

/// <summary>
/// Envia correos por SMTP. Si no hay servidor configurado (Email:Host vacio),
/// registra el contenido en el log como fallback para desarrollo.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(EmailSettings settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning(
                "SMTP no configurado (Email:Host vacio). Correo NO enviado a {To}. Asunto: {Subject}\n{Body}",
                to, subject, htmlBody);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.From, _settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            Credentials = new NetworkCredential(_settings.User, _settings.Password),
            Timeout = Math.Max(1, _settings.TimeoutSeconds) * 1000,
        };

        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Correo enviado a {To} (asunto: {Subject}).", to, subject);
    }
}
