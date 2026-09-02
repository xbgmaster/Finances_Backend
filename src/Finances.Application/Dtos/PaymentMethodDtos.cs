using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

/// <summary>
/// A payment method with its derived spending figures. Balance and available credit are
/// computed from the linked expenses (credit cards only).
/// </summary>
public record PaymentMethodDto(
    int Id,
    string Name,
    string Type,
    string Currency,
    string Color,
    string Icon,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay,
    bool Archived,
    bool IsFavorite,
    // How much was charged to this method in the current month (its own currency).
    decimal SpentThisMonth,
    // How much income landed in this method in the current month (debit/cash).
    decimal ReceivedThisMonth,
    // Debit/cash: money currently in the account (income - expenses).
    // Credit card: outstanding balance owed (charges minus payments).
    decimal Balance,
    // For credit cards: CreditLimit - Balance. Null for debit/cash.
    decimal? AvailableCredit);

public class PaymentMethodCreateDto
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>One of "Debit", "Cash", "CreditCard". Defaults to Debit.</summary>
    [MaxLength(20)]
    public string Type { get; set; } = "Debit";

    /// <summary>ISO currency code. Null/empty falls back to the user's base currency.</summary>
    [MaxLength(3)]
    public string? Currency { get; set; }

    [MaxLength(9)]
    public string Color { get; set; } = "#6366f1";

    [MaxLength(60)]
    public string Icon { get; set; } = "card";

    /// <summary>Credit limit (cupo). Required for credit cards, ignored otherwise.</summary>
    public decimal? CreditLimit { get; set; }

    [Range(1, 31)]
    public int? StatementDay { get; set; }

    [Range(1, 31)]
    public int? PaymentDueDay { get; set; }

    public bool Archived { get; set; }

    /// <summary>Mark as the preferred method (unsets any previous favorite).</summary>
    public bool IsFavorite { get; set; }
}
