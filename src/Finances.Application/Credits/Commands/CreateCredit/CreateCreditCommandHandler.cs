using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using MediatR;

namespace Finances.Application.Credits.Commands.CreateCredit;

public class CreateCreditCommandHandler : IRequestHandler<CreateCreditCommand, CreditDto>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public CreateCreditCommandHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CreditDto> Handle(CreateCreditCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credit = new Credit
        {
            Name = request.Name.Trim(),
            Type = Enum.Parse<CreditType>(request.Type, ignoreCase: true),
            Principal = request.Principal,
            AnnualInterestRate = request.AnnualInterestRate,
            TermMonths = request.TermMonths,
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim().ToUpperInvariant(),
            UserId = userId
        };

        _db.Credits.Add(credit);
        await _db.SaveChangesAsync(cancellationToken);

        // A brand new credit has no payments yet, so total paid is zero.
        return CreditMapper.ToListItem(credit, totalPaid: 0m);
    }
}
