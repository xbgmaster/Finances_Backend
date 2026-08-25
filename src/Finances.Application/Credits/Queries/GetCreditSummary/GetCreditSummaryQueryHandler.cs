using Finances.Application.Common;
using Finances.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Queries.GetCreditSummary;

public class GetCreditSummaryQueryHandler : IRequestHandler<GetCreditSummaryQuery, CreditSummaryDto?>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public GetCreditSummaryQueryHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CreditSummaryDto?> Handle(GetCreditSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken);
        if (credit is null) return null;

        var payments = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .ToListAsync(cancellationToken);

        return CreditMapper.ToSummary(credit, payments, DateTime.UtcNow);
    }
}
