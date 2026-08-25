using System.Globalization;
using System.Text;
using Finances.Application.Common;
using Finances.Application.Credits;
using Finances.Application.Dtos;
using Finances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Notifications;

/// <summary>
/// Background job that periodically scans every user's credits and emails a reminder when
/// an installment is about to be due or is already overdue. A per-credit dedupe key stored
/// on the credit ensures each distinct alert is emailed only once (no daily spam); a fresh
/// due date or an escalation from "due soon" to "overdue" produces a new key and a new email.
/// </summary>
public class CreditReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CreditReminderOptions _options;
    private readonly ILogger<CreditReminderService> _logger;

    public CreditReminderService(
        IServiceScopeFactory scopeFactory,
        CreditReminderOptions options,
        ILogger<CreditReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var period = TimeSpan.FromHours(Math.Max(0.1, _options.CheckEveryHours));
        using var timer = new PeriodicTimer(period);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Credit reminder run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var urls = scope.ServiceProvider.GetRequiredService<AppUrls>();

        var credits = await db.Credits.ToListAsync(ct);
        if (credits.Count == 0) return;

        var paidByCredit = await db.CreditPayments
            .GroupBy(p => p.CreditId)
            .Select(g => new { CreditId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.CreditId, x => x.Total, ct);

        var asOf = DateTime.UtcNow;

        // Pair every alerting credit with its entity so we can update the dedupe key.
        var alerting = credits
            .Select(c => new
            {
                Entity = c,
                Dto = CreditMapper.ToListItem(c, paidByCredit.GetValueOrDefault(c.Id), asOf),
            })
            .Where(x => x.Dto.Status == "Active" && (x.Dto.IsOverdue || x.Dto.IsDueSoon))
            .ToList();

        if (alerting.Count == 0) return;

        var byUser = alerting.GroupBy(x => x.Entity.UserId).ToList();

        var userIds = byUser.Select(g => g.Key).ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id) && u.Email != null)
            .Select(u => new { u.Id, u.Email, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => (u.Email!, u.FullName), ct);

        var anySent = false;

        foreach (var group in byUser)
        {
            if (!users.TryGetValue(group.Key, out var user)) continue;

            var items = group
                .OrderByDescending(x => x.Dto.IsOverdue)
                .ThenBy(x => x.Dto.DaysUntilDue)
                .ToList();

            // Only email when at least one credit represents a *new* alert state.
            var hasNew = items.Any(x => x.Entity.LastReminderKey != KeyFor(x.Dto));
            if (!hasNew) continue;

            var (subject, body) = BuildEmail(user.Item2, items.Select(x => x.Dto).ToList(), urls);

            try
            {
                await email.SendAsync(user.Item1, subject, body, ct);
                foreach (var x in items)
                    x.Entity.LastReminderKey = KeyFor(x.Dto);
                anySent = true;
                _logger.LogInformation("Credit reminder sent to {Email} ({Count} credit(s)).", user.Item1, items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send credit reminder to {Email}.", user.Item1);
            }
        }

        if (anySent)
            await db.SaveChangesAsync(ct);
    }

    private static string KeyFor(CreditDto dto) =>
        $"{dto.NextDueDate:yyyyMMdd}:{dto.AlertLevel}";

    private static (string Subject, string Body) BuildEmail(string? fullName, IReadOnlyList<CreditDto> items, AppUrls urls)
    {
        var overdue = items.Where(i => i.IsOverdue).ToList();
        var dueSoon = items.Where(i => i.IsDueSoon).ToList();

        var subject = overdue.Count > 0
            ? $"Action needed: {overdue.Count} credit payment(s) overdue"
            : "Reminder: you have a credit payment due soon";

        var link = $"{urls.FrontendBaseUrl.TrimEnd('/')}/credits";
        var greeting = string.IsNullOrWhiteSpace(fullName) ? "Hi," : $"Hi {fullName},";

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:auto;color:#0f172a\">");
        sb.Append("<h2 style=\"margin-bottom:4px\">Credit payment reminder</h2>");
        sb.Append($"<p>{greeting}</p>");
        sb.Append("<p>Here is the status of your upcoming and overdue credit payments:</p>");

        if (overdue.Count > 0)
        {
            sb.Append("<h3 style=\"color:#ef4444;margin-bottom:6px\">Overdue</h3>");
            sb.Append("<ul style=\"padding-left:18px;margin-top:0\">");
            foreach (var c in overdue)
            {
                var days = Math.Abs(c.DaysUntilDue);
                sb.Append($"<li style=\"margin-bottom:6px\"><strong>{Escape(c.Name)}</strong> — installment {Money(c.MonthlyInstallment, c.Currency)}, due {Date(c.NextDueDate)} ({days} day(s) ago)</li>");
            }
            sb.Append("</ul>");
        }

        if (dueSoon.Count > 0)
        {
            sb.Append("<h3 style=\"color:#f59e0b;margin-bottom:6px\">Due soon</h3>");
            sb.Append("<ul style=\"padding-left:18px;margin-top:0\">");
            foreach (var c in dueSoon)
            {
                var when = c.DaysUntilDue == 0 ? "today" : $"in {c.DaysUntilDue} day(s)";
                sb.Append($"<li style=\"margin-bottom:6px\"><strong>{Escape(c.Name)}</strong> — installment {Money(c.MonthlyInstallment, c.Currency)}, due {Date(c.NextDueDate)} ({when})</li>");
            }
            sb.Append("</ul>");
        }

        sb.Append($"<p style=\"text-align:center;margin:28px 0\"><a href=\"{link}\" style=\"background:#6366f1;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:bold;display:inline-block\">Review my credits</a></p>");
        sb.Append("<hr/>");
        sb.Append("<p style=\"color:#64748b;font-size:13px\">Once you register the payment in the app, this reminder stops automatically.</p>");
        sb.Append("</div>");

        return (subject, sb.ToString());
    }

    private static string Money(decimal amount, string? currency)
    {
        var formatted = amount.ToString("N2", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(currency) ? formatted : $"{currency} {formatted}";
    }

    private static string Date(DateTime date) =>
        date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
