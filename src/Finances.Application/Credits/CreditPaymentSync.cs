using Finances.Application.Common;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits;

/// <summary>
/// Shared logic that mirrors a credit payment as a real expense so it reduces the user's
/// available balance (in the credit's own currency) and shows up in the spending reports.
/// Cross-currency funding is handled separately via explicit currency exchanges.
/// </summary>
public static class CreditPaymentSync
{
    /// <summary>Currency for the mirrored expense: the credit's own currency, else the base currency.</summary>
    public static string ExpenseCurrency(Credit credit, string baseCurrency) =>
        !string.IsNullOrWhiteSpace(credit.Currency) ? credit.Currency!.ToUpperInvariant() : baseCurrency;

    public static string Describe(Credit credit, CreditPayment payment) =>
        $"{credit.Name} - {(payment.Type == CreditPaymentType.PrincipalPrepayment ? "principal prepayment" : "installment")}";

    /// <summary>Builds the mirrored expense linked to the payment (uses navigation properties so it
    /// works before the payment has an id).</summary>
    public static Expense BuildMirrorExpense(
        Credit credit, CreditPayment payment, Category category, string baseCurrency, string userId) => new()
    {
        Amount = payment.Amount,
        Description = Describe(credit, payment),
        Date = payment.Date,
        Category = category,
        CreditPayment = payment,
        Currency = ExpenseCurrency(credit, baseCurrency),
        UserId = userId
    };

    /// <summary>Gets the user's system "Debt payments" category, creating it lazily if missing.</summary>
    public static async Task<Category> GetOrCreateDebtCategoryAsync(
        IFinanceDbContext db, string userId, CancellationToken ct)
    {
        var existing = await db.Categories
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
        db.Categories.Add(category);
        return category;
    }
}
