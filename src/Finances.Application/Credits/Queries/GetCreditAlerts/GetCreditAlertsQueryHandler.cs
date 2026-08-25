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

        return new CreditAlertsDto(overdue, dueSoon, alerting);
    }
}
