using Finances.Application.Common;
using Finances.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Queries.GetAmortizationSchedule;

public class GetAmortizationScheduleQueryHandler
    : IRequestHandler<GetAmortizationScheduleQuery, CreditScheduleDto?>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public GetAmortizationScheduleQueryHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CreditScheduleDto?> Handle(
        GetAmortizationScheduleQuery request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken);
        if (credit is null) return null;

        var payments = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .ToListAsync(cancellationToken);

        return CreditMapper.ToSchedule(credit, payments);
    }
}
