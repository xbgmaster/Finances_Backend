using Finances.Application.Common;
using Finances.Application.Dtos;
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

        var paidByCredit = await _db.CreditPayments
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.CreditId)
            .Select(g => new { CreditId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.CreditId, x => x.Total, cancellationToken);

        var asOf = DateTime.UtcNow;
        return credits
            .Select(c => CreditMapper.ToListItem(c, paidByCredit.GetValueOrDefault(c.Id), asOf))
            .ToList();
    }
}
