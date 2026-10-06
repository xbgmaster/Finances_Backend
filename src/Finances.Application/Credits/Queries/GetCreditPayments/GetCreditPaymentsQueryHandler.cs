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
        return await (
            from p in _db.CreditPayments
            where p.CreditId == request.CreditId && p.UserId == userId
            join e in _db.Expenses on p.Id equals e.CreditPaymentId into mirrors
            from e in mirrors.DefaultIfEmpty()
            orderby p.Date descending, p.Id descending
            select new CreditPaymentDto(
                p.Id, p.Amount, p.Date, p.Note,
                p.Type.ToString(),
                p.Effect != null ? p.Effect.ToString() : null,
                e != null ? e.PaymentMethodId : null,
                e != null && e.PaymentMethod != null ? e.PaymentMethod.Name : null))
            .ToListAsync(cancellationToken);
    }
}
