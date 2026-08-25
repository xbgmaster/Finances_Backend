using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Commands.UpdatePayment;

public class UpdatePaymentCommandHandler : IRequestHandler<UpdatePaymentCommand, CreditSummaryDto>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public UpdatePaymentCommandHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CreditSummaryDto> Handle(UpdatePaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var payment = await _db.CreditPayments
            .FirstOrDefaultAsync(
                p => p.Id == request.PaymentId && p.CreditId == request.CreditId && p.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("El pago no existe.");

        var type = Enum.Parse<CreditPaymentType>(request.Type, ignoreCase: true);
        payment.Amount = request.Amount;
        payment.Date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        payment.Type = type;
        payment.Effect = type == CreditPaymentType.PrincipalPrepayment && !string.IsNullOrWhiteSpace(request.Effect)
            ? Enum.Parse<PrepaymentEffect>(request.Effect, ignoreCase: true)
            : null;
        payment.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.CreditId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("El credito no existe.");

        var payments = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .ToListAsync(cancellationToken);

        return CreditMapper.ToSummary(credit, payments, DateTime.UtcNow);
    }
}
