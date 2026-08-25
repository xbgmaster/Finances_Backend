using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Commands.UpdateCredit;

public class UpdateCreditCommandHandler : IRequestHandler<UpdateCreditCommand, CreditSummaryDto?>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public UpdateCreditCommandHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CreditSummaryDto?> Handle(UpdateCreditCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken);
        if (credit is null) return null;

        credit.Name = request.Name.Trim();
        credit.Type = Enum.Parse<CreditType>(request.Type, ignoreCase: true);
        credit.InterestModel = Enum.Parse<InterestModel>(request.InterestModel, ignoreCase: true);
        credit.PrepaymentEffect = Enum.Parse<PrepaymentEffect>(request.PrepaymentEffect, ignoreCase: true);
        credit.PrepaymentPenaltyRate = request.PrepaymentPenaltyRate;
        credit.Principal = request.Principal;
        credit.AnnualInterestRate = request.AnnualInterestRate;
        credit.TermMonths = request.TermMonths;
        credit.StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        credit.PaymentDueDay = request.PaymentDueDay;
        credit.Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim().ToUpperInvariant();

        // The terms/due day may have changed, so any previous reminder key no longer applies;
        // reset it so the next scan re-evaluates the alert cleanly.
        credit.LastReminderKey = null;

        await _db.SaveChangesAsync(cancellationToken);

        var totalPaid = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return CreditMapper.ToSummary(credit, totalPaid, DateTime.UtcNow);
    }
}
