using Finances.Application.Common;
using Finances.Application.Credits;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Finances.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Commands.UpdatePayment;

public class UpdatePaymentCommandHandler : IRequestHandler<UpdatePaymentCommand, CreditSummaryDto>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public UpdatePaymentCommandHandler(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<CreditSummaryDto> Handle(UpdatePaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var payment = await _db.CreditPayments
            .FirstOrDefaultAsync(
                p => p.Id == request.PaymentId && p.CreditId == request.CreditId && p.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("El pago no existe.");

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.CreditId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("El credito no existe.");

        var baseCurrency = (await _profile.GetAsync(cancellationToken)).Currency;

        var type = Enum.Parse<CreditPaymentType>(request.Type, ignoreCase: true);
        payment.Amount = request.Amount;
        payment.Date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        payment.Type = type;
        payment.Effect = type == CreditPaymentType.PrincipalPrepayment && !string.IsNullOrWhiteSpace(request.Effect)
            ? Enum.Parse<PrepaymentEffect>(request.Effect, ignoreCase: true)
            : null;
        payment.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await SyncMirroredExpenseAsync(credit, payment, baseCurrency, userId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var payments = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .ToListAsync(cancellationToken);

        return CreditMapper.ToSummary(credit, payments, DateTime.UtcNow);
    }

    private async Task SyncMirroredExpenseAsync(
        Credit credit, CreditPayment payment, string baseCurrency, string userId, CancellationToken ct)
    {
        var mirror = await _db.Expenses
            .FirstOrDefaultAsync(e => e.CreditPaymentId == payment.Id && e.UserId == userId, ct);

        if (mirror is not null)
        {
            mirror.Amount = payment.Amount;
            mirror.Currency = CreditPaymentSync.ExpenseCurrency(credit, baseCurrency);
            mirror.Date = payment.Date;
            mirror.Description = CreditPaymentSync.Describe(credit, payment);
            return;
        }

        // No mirror yet (legacy payment created before this feature): create one now.
        var category = await CreditPaymentSync.GetOrCreateDebtCategoryAsync(_db, userId, ct);
        _db.Expenses.Add(CreditPaymentSync.BuildMirrorExpense(credit, payment, category, baseCurrency, userId));
    }
}
