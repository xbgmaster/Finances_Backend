using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Queries.GetCreditAlerts;

public class GetCreditAlertsQueryHandler : IRequestHandler<GetCreditAlertsQuery, CreditAlertsDto>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public GetCreditAlertsQueryHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CreditAlertsDto> Handle(GetCreditAlertsQuery request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credits = await _db.Credits
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        var paymentsByCredit = (await _db.CreditPayments
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken))
            .GroupBy(p => p.CreditId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CreditPayment>)g.ToList());

        var empty = (IReadOnlyList<CreditPayment>)Array.Empty<CreditPayment>();
        var asOf = DateTime.UtcNow;

        // Reuse the exact same alert derivation the rest of the app uses.
        var alerting = credits
            .Select(c => CreditMapper.ToListItem(c, paymentsByCredit.GetValueOrDefault(c.Id, empty), asOf))
            .Where(c => c.Status == "Active" && (c.IsOverdue || c.IsDueSoon))
            // Overdue first, then whatever is closest to its due date.
            .OrderByDescending(c => c.IsOverdue)
            .ThenBy(c => c.DaysUntilDue)
            .Select(c => new CreditAlertItemDto(
                c.Id, c.Name, c.AlertLevel, c.NextDueDate, c.DaysUntilDue, c.MonthlyInstallment, c.Currency))
            .ToList();

        var overdue = alerting.Count(a => a.AlertLevel == "Overdue");
        var dueSoon = alerting.Count - overdue;

        // ---- Credit-card alerts: over-limit + approaching statement/payment dates ----
        var cards = await _db.PaymentMethods
            .Where(p => p.UserId == userId && p.Type == Domain.Entities.PaymentMethodType.CreditCard && !p.Archived)
            .ToListAsync(cancellationToken);

        var cardAlerts = new List<CardAlertItemDto>();
        if (cards.Count > 0)
        {
            var cardIds = cards.Select(c => c.Id).ToList();
            var chargesByCard = await _db.Expenses
                .Where(e => e.UserId == userId && e.PaymentMethodId != null && cardIds.Contains(e.PaymentMethodId.Value))
                .GroupBy(e => e.PaymentMethodId!.Value)
                .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync(cancellationToken);
            var paymentsByCard = await _db.CardPayments
                .Where(p => p.UserId == userId && cardIds.Contains(p.CreditCardId))
                .GroupBy(p => p.CreditCardId)
                .Select(g => new { Id = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync(cancellationToken);

            var chargeMap  = chargesByCard.ToDictionary(x => x.Id, x => x.Total);
            var paymentMap = paymentsByCard.ToDictionary(x => x.Id, x => x.Total);
            var today = asOf.Day;

            foreach (var card in cards)
            {
                if (card.CreditLimit is null || card.CreditLimit <= 0) continue;

                var charged = chargeMap.GetValueOrDefault(card.Id);
                var paid    = paymentMap.GetValueOrDefault(card.Id);
                var debt    = charged - paid;
                var limit   = card.CreditLimit.Value;

                if (debt > limit)
                    cardAlerts.Add(new CardAlertItemDto(card.Id, card.Name, "OverLimit",
                        Math.Round(debt - limit, 2), null, card.Currency ?? ""));

                // Statement day within next 7 days.
                if (card.StatementDay is int stDay)
                {
                    var days = (stDay - today + 31) % 31;
                    if (days is >= 0 and <= 7)
                        cardAlerts.Add(new CardAlertItemDto(card.Id, card.Name, "StatementSoon",
                            null, days, card.Currency ?? ""));
                }
                // Payment due day within next 7 days.
                if (card.PaymentDueDay is int dueDay)
                {
                    var days = (dueDay - today + 31) % 31;
                    if (days is >= 0 and <= 7)
                        cardAlerts.Add(new CardAlertItemDto(card.Id, card.Name, "PaymentSoon",
                            null, days, card.Currency ?? ""));
                }
            }
        }

        return new CreditAlertsDto(overdue, dueSoon, alerting, cardAlerts.Count, cardAlerts);
    }
}
