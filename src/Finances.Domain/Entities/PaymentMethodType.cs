namespace Finances.Domain.Entities;

/// <summary>
/// Kind of payment method / account a transaction can be tied to. Drives whether an
/// expense is cash-out immediately (debit/cash) or adds to a revolving card balance.
/// </summary>
public enum PaymentMethodType
{
    /// <summary>Debit card or bank account. Spending reduces available cash right away.</summary>
    Debit = 0,

    /// <summary>Physical cash. Behaves like debit for cash-flow purposes.</summary>
    Cash = 1,

    /// <summary>Revolving credit card with a credit limit (cupo). Charges add to its balance.</summary>
    CreditCard = 2
}
