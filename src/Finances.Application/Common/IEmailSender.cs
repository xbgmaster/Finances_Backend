namespace Finances.Application.Common;

/// <summary>Envio de correos. Implementado por Infrastructure (SMTP).</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
