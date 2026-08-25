using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Commands.RegisterPayment;

public class RegisterPaymentCommandHandler : IRequestHandler<RegisterPaymentCommand, CreditSummaryDto>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public RegisterPaymentCommandHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CreditSummaryDto> Handle(RegisterPaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.CreditId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("El credito no existe.");

        var type = Enum.Parse<CreditPaymentType>(request.Type, ignoreCase: true);
        var effect = type == CreditPaymentType.PrincipalPrepayment && !string.IsNullOrWhiteSpace(request.Effect)
            ? Enum.Parse<PrepaymentEffect>(request.Effect, ignoreCase: true)
            : (PrepaymentEffect?)null;

        var payment = new CreditPayment
        {
            CreditId = credit.Id,
            Amount = request.Amount,
            Date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc),
            Type = type,
            Effect = effect,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            UserId = userId
        };

        _db.CreditPayments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        var payments = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .ToListAsync(cancellationToken);

        return CreditMapper.ToSummary(credit, payments, DateTime.UtcNow);
    }
}
