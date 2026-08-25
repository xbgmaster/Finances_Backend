using Finances.Application.Common;
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

        var type = Enum.Parse<CreditPaymentType>(request.Type, ignoreCase: true);
        payment.Amount = request.Amount;
        payment.Date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        payment.Type = type;
        payment.Effect = type == CreditPaymentType.PrincipalPrepayment && !string.IsNullOrWhiteSpace(request.Effect)
            ? Enum.Parse<PrepaymentEffect>(request.Effect, ignoreCase: true)
            : null;
        payment.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.CreditId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("El credito no existe.");

        await SyncMirroredExpenseAsync(credit, payment, userId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var payments = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .ToListAsync(cancellationToken);

        return CreditMapper.ToSummary(credit, payments, DateTime.UtcNow);
    }

    private async Task SyncMirroredExpenseAsync(
        Credit credit, CreditPayment payment, string userId, CancellationToken ct)
    {
        var mirror = await _db.Expenses
            .FirstOrDefaultAsync(e => e.CreditPaymentId == payment.Id && e.UserId == userId, ct);

        var description =
            $"{credit.Name} - {(payment.Type == CreditPaymentType.PrincipalPrepayment ? "principal prepayment" : "installment")}";

        if (mirror is not null)
        {
            mirror.Amount = payment.Amount;
            mirror.Date = payment.Date;
            mirror.Description = description;
            return;
        }

        // No mirror yet (e.g. the credit's currency was set to match the base after the
        // payment was created). Create one now if the currencies line up.
        var profile = await _profile.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(credit.Currency) ||
            !string.Equals(credit.Currency, profile.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var category = await GetOrCreateDebtCategoryAsync(userId, ct);
        _db.Expenses.Add(new Expense
        {
            Amount = payment.Amount,
            Description = description,
            Date = payment.Date,
            Category = category,
            CreditPayment = payment,
            UserId = userId
        });
    }

    private async Task<Category> GetOrCreateDebtCategoryAsync(string userId, CancellationToken ct)
    {
        var existing = await _db.Categories
            .FirstOrDefaultAsync(c => c.UserId == userId && c.IsSystem
                && c.Name == Category.DebtPaymentsSystemName, ct);
        if (existing is not null) return existing;

        var category = new Category
        {
            Name = Category.DebtPaymentsSystemName,
            Icon = "bank",
            Color = "#ef4444",
            MonthlyBudget = null,
            IsSystem = true,
            UserId = userId
        };
        _db.Categories.Add(category);
        return category;
    }
}
