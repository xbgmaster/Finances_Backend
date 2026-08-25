using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Finances.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Commands.RegisterPayment;

public class RegisterPaymentCommandHandler : IRequestHandler<RegisterPaymentCommand, CreditSummaryDto>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public RegisterPaymentCommandHandler(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
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

        // Mirror the outflow as a real expense so it reduces the available balance and shows
        // up in the spending reports, but only when the credit is denominated in the user's
        // base currency (we do not convert across currencies).
        await MirrorAsExpenseAsync(credit, payment, userId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var payments = await _db.CreditPayments
            .Where(p => p.CreditId == credit.Id)
            .ToListAsync(cancellationToken);

        return CreditMapper.ToSummary(credit, payments, DateTime.UtcNow);
    }

    private async Task MirrorAsExpenseAsync(
        Credit credit, CreditPayment payment, string userId, CancellationToken ct)
    {
        var profile = await _profile.GetAsync(ct);
        var baseCurrency = profile.Currency;

        // No credit currency, or a currency that differs from the base one: skip mirroring.
        if (string.IsNullOrWhiteSpace(credit.Currency) ||
            !string.Equals(credit.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var category = await GetOrCreateDebtCategoryAsync(userId, ct);

        var expense = new Expense
        {
            Amount = payment.Amount,
            Description = $"{credit.Name} - {(payment.Type == CreditPaymentType.PrincipalPrepayment ? "principal prepayment" : "installment")}",
            Date = payment.Date,
            Category = category,
            CreditPayment = payment,
            UserId = userId
        };

        _db.Expenses.Add(expense);
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
