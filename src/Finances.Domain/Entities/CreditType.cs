namespace Finances.Domain.Entities;

/// <summary>Type of credit/obligation. Drives which calculation model applies.</summary>
public enum CreditType
{
    /// <summary>Credit card (revolving). MVP treats it like a fixed-term loan.</summary>
    CreditCard = 0,

    /// <summary>Loan with interest conditions (fixed term).</summary>
    InterestLoan = 1,

    /// <summary>Free-investment loan (fixed term).</summary>
    LibreInversion = 2
}
