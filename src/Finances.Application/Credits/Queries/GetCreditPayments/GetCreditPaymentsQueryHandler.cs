using Finances.Application.Common;
using Finances.Application.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Queries.GetCreditPayments;

public class GetCreditPaymentsQueryHandler
    : IRequestHandler<GetCreditPaymentsQuery, IReadOnlyList<CreditPaymentDto>>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public GetCreditPaymentsQueryHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<CreditPaymentDto>> Handle(
        GetCreditPaymentsQuery request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();
        return await _db.CreditPayments
            .Where(p => p.CreditId == request.CreditId && p.UserId == userId)
            .OrderByDescending(p => p.Date)
            .ThenByDescending(p => p.Id)
            .Select(p => new CreditPaymentDto(p.Id, p.Amount, p.Date, p.Note))
            .ToListAsync(cancellationToken);
    }
}
