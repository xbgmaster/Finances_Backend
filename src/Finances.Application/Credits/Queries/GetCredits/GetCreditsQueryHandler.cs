using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Queries.GetCredits;

public class GetCreditsQueryHandler : IRequestHandler<GetCreditsQuery, IReadOnlyList<CreditDto>>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public GetCreditsQueryHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<CreditDto>> Handle(GetCreditsQuery request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credits = await _db.Credits
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var paymentsByCredit = (await _db.CreditPayments
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken))
            .GroupBy(p => p.CreditId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CreditPayment>)g.ToList());

        var empty = (IReadOnlyList<CreditPayment>)Array.Empty<CreditPayment>();
        var asOf = DateTime.UtcNow;
        return credits
            .Select(c => CreditMapper.ToListItem(c, paymentsByCredit.GetValueOrDefault(c.Id, empty), asOf))
            .ToList();
    }
}
