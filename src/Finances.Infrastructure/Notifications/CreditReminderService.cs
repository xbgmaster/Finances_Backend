using System.Globalization;
using System.Text;
using Finances.Application.Common;
using Finances.Application.Credits;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Finances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Notifications;

/// <summary>
/// Background job that emails reminders for loan credits and credit-card payment due dates.
/// Per-item <c>LastReminderKey</c> dedupes so each distinct alert state is emailed once.
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

        var asOf = DateTime.UtcNow;
        var byUser = new Dictionary<string, UserReminderBundle>(StringComparer.Ordinal);

        // ---- Loan credits ----
        var credits = await db.Credits.ToListAsync(ct);
        if (credits.Count > 0)
        {
            var paymentsByCredit = (await db.CreditPayments.ToListAsync(ct))
                .GroupBy(p => p.CreditId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<CreditPayment>)g.ToList());
            var empty = (IReadOnlyList<CreditPayment>)Array.Empty<CreditPayment>();

            foreach (var c in credits)
            {
                var dto = CreditMapper.ToListItem(c, paymentsByCredit.GetValueOrDefault(c.Id, empty), asOf);
                if (dto.Status != "Active" || (!dto.IsOverdue && !dto.IsDueSoon)) continue;

                var key = $"{dto.NextDueDate:yyyyMMdd}:{dto.AlertLevel}";
                if (c.LastReminderKey == key) continue;

                var bundle = GetBundle(byUser, c.UserId);
                bundle.Credits.Add((c, dto, key));
            }
        }

        // ---- Credit cards (payment due day) ----
        var cards = await db.PaymentMethods
            .Where(p => p.Type == PaymentMethodType.CreditCard && !p.Archived && p.PaymentDueDay != null)
            .ToListAsync(ct);
        if (cards.Count > 0)
        {
            var cardIds = cards.Select(c => c.Id).ToList();
            var payments = await db.CardPayments
                .Where(p => cardIds.Contains(p.CreditCardId))
                .Select(p => new { p.CreditCardId, p.Date })
                .ToListAsync(ct);
            var paysByCard = payments
                .GroupBy(p => p.CreditCardId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Date).ToList());

            foreach (var card in cards)
            {
                var status = CardDueStatus(card, paysByCard.GetValueOrDefault(card.Id), asOf);
                if (status is null) continue;
                if (status.Value.Paid) continue;
                // Email when due within 7 days or already overdue (same window as in-app card alerts).
                if (status.Value.DaysUntil > 7) continue;

                var level = status.Value.DaysUntil < 0 ? "Overdue" : "PaymentSoon";
                var key = $"{status.Value.DueDate:yyyyMMdd}:{level}";
                if (card.LastReminderKey == key) continue;

                var bundle = GetBundle(byUser, card.UserId);
                bundle.Cards.Add((card, status.Value, key));
            }
        }

        if (byUser.Count == 0) return;

        var userIds = byUser.Keys.ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id) && u.Email != null)
            .Select(u => new { u.Id, u.Email, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => (u.Email!, u.FullName), ct);

        var anySent = false;

        foreach (var (userId, bundle) in byUser)
        {
            if (!users.TryGetValue(userId, out var user)) continue;
            if (bundle.Credits.Count == 0 && bundle.Cards.Count == 0) continue;

            var (subject, body) = BuildEmail(
                user.Item2,
                bundle.Credits.Select(x => x.Dto).ToList(),
                bundle.Cards.Select(x => (x.Card.Name, x.Status, x.Card.Currency)).ToList(),
                urls);

            try
            {
                await email.SendAsync(user.Item1, subject, body, ct);
                foreach (var x in bundle.Credits)
                    x.Entity.LastReminderKey = x.Key;
                foreach (var x in bundle.Cards)
                    x.Card.LastReminderKey = x.Key;
                anySent = true;
                _logger.LogInformation(
                    "Payment reminder sent to {Email} ({Credits} credit(s), {Cards} card(s)).",
                    user.Item1, bundle.Credits.Count, bundle.Cards.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment reminder to {Email}.", user.Item1);
            }
        }

        if (anySent)
            await db.SaveChangesAsync(ct);
    }

    private static UserReminderBundle GetBundle(Dictionary<string, UserReminderBundle> map, string userId)
    {
        if (!map.TryGetValue(userId, out var bundle))
        {
            bundle = new UserReminderBundle();
            map[userId] = bundle;
        }
        return bundle;
    }

    private sealed class UserReminderBundle
    {
        public List<(Credit Entity, CreditDto Dto, string Key)> Credits { get; } = new();
        public List<(PaymentMethod Card, CardDueInfo Status, string Key)> Cards { get; } = new();
    }

    private readonly record struct CardDueInfo(DateTime DueDate, bool Paid, int DaysUntil);

    /// <summary>Mirrors the Cards UI cycle logic (late payments clear the past due).</summary>
    private static CardDueInfo? CardDueStatus(PaymentMethod card, List<DateTime>? payments, DateTime today)
    {
        if (card.PaymentDueDay is not int dueDay) return null;

        static DateTime StartOfDay(DateTime value) =>
            new(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Unspecified);

        static DateTime Clamp(int y, int m, int day)
        {
            var dim = DateTime.DaysInMonth(y, m);
            return StartOfDay(new DateTime(y, m, Math.Min(Math.Max(1, day), dim)));
        }

        var today0 = StartOfDay(today);
        var due = Clamp(today0.Year, today0.Month, dueDay);
        var prev = Clamp(due.Year, due.Month - 1, dueDay);
        var next = Clamp(due.Year, due.Month + 1, dueDay);

        var pool = (payments ?? new List<DateTime>())
            .Select(StartOfDay)
            .OrderBy(d => d)
            .ToList();
        var payIdx = 0;

        bool Consume(bool allowLate)
        {
            while (payIdx < pool.Count)
            {
                var d = pool[payIdx];
                if (d <= prev) { payIdx++; continue; }
                if (d <= due || (allowLate && d <= next)) { payIdx++; return true; }
                return false;
            }
            return false;
        }

        while (today0 > due)
        {
            if (!Consume(true)) break;
            prev = due;
            due = next;
            next = Clamp(due.Year, due.Month + 1, dueDay);
        }

        var paid = Consume(today0 > due);
        var daysUntil = (int)Math.Round((due - today0).TotalDays);
        return new CardDueInfo(due, paid, daysUntil);
    }

    private static (string Subject, string Body) BuildEmail(
        string? fullName,
        IReadOnlyList<CreditDto> credits,
        IReadOnlyList<(string Name, CardDueInfo Status, string? Currency)> cards,
        AppUrls urls)
    {
        var overdueCredits = credits.Where(i => i.IsOverdue).ToList();
        var dueSoonCredits = credits.Where(i => i.IsDueSoon).ToList();
        var overdueCards = cards.Where(c => c.Status.DaysUntil < 0).ToList();
        var dueSoonCards = cards.Where(c => c.Status.DaysUntil >= 0).ToList();

        var anyOverdue = overdueCredits.Count > 0 || overdueCards.Count > 0;
        var subject = anyOverdue
            ? "Action needed: you have overdue payment(s)"
            : "Reminder: you have a payment due soon";

        var creditsLink = $"{urls.FrontendBaseUrl.TrimEnd('/')}/credits";
        var cardsLink = $"{urls.FrontendBaseUrl.TrimEnd('/')}/cards";
        var greeting = string.IsNullOrWhiteSpace(fullName) ? "Hi," : $"Hi {fullName},";

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:auto;color:#0f172a\">");
        sb.Append("<h2 style=\"margin-bottom:4px\">Payment reminder</h2>");
        sb.Append($"<p>{greeting}</p>");
        sb.Append("<p>Here is the status of your upcoming and overdue payments:</p>");

        if (overdueCredits.Count > 0 || overdueCards.Count > 0)
        {
            sb.Append("<h3 style=\"color:#ef4444;margin-bottom:6px\">Overdue</h3>");
            sb.Append("<ul style=\"padding-left:18px;margin-top:0\">");
            foreach (var c in overdueCredits)
            {
                var days = Math.Abs(c.DaysUntilDue);
                sb.Append($"<li style=\"margin-bottom:6px\"><strong>{Escape(c.Name)}</strong> (loan) — installment {Money(c.MonthlyInstallment, c.Currency)}, due {Date(c.NextDueDate)} ({days} day(s) ago)</li>");
            }
            foreach (var c in overdueCards)
            {
                var days = Math.Abs(c.Status.DaysUntil);
                sb.Append($"<li style=\"margin-bottom:6px\"><strong>{Escape(c.Name)}</strong> (card) — payment due {Date(c.Status.DueDate)} ({days} day(s) ago)</li>");
            }
            sb.Append("</ul>");
        }

        if (dueSoonCredits.Count > 0 || dueSoonCards.Count > 0)
        {
            sb.Append("<h3 style=\"color:#f59e0b;margin-bottom:6px\">Due soon</h3>");
            sb.Append("<ul style=\"padding-left:18px;margin-top:0\">");
            foreach (var c in dueSoonCredits)
            {
                var when = c.DaysUntilDue == 0 ? "today" : $"in {c.DaysUntilDue} day(s)";
                sb.Append($"<li style=\"margin-bottom:6px\"><strong>{Escape(c.Name)}</strong> (loan) — installment {Money(c.MonthlyInstallment, c.Currency)}, due {Date(c.NextDueDate)} ({when})</li>");
            }
            foreach (var c in dueSoonCards)
            {
                var when = c.Status.DaysUntil == 0 ? "today" : $"in {c.Status.DaysUntil} day(s)";
                sb.Append($"<li style=\"margin-bottom:6px\"><strong>{Escape(c.Name)}</strong> (card) — payment due {Date(c.Status.DueDate)} ({when})</li>");
            }
            sb.Append("</ul>");
        }

        sb.Append("<p style=\"text-align:center;margin:28px 0\">");
        if (credits.Count > 0)
            sb.Append($"<a href=\"{creditsLink}\" style=\"background:#0f5c4d;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:bold;display:inline-block;margin:4px\">Review credits</a>");
        if (cards.Count > 0)
            sb.Append($"<a href=\"{cardsLink}\" style=\"background:#0f5c4d;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:bold;display:inline-block;margin:4px\">Review cards</a>");
        sb.Append("</p>");
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
