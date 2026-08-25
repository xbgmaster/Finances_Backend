using Finances.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Commands.DeletePayment;

public class DeletePaymentCommandHandler : IRequestHandler<DeletePaymentCommand>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public DeletePaymentCommandHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task Handle(DeletePaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var payment = await _db.CreditPayments
            .FirstOrDefaultAsync(
                p => p.Id == request.PaymentId && p.CreditId == request.CreditId && p.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("El pago no existe.");

        _db.CreditPayments.Remove(payment);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
