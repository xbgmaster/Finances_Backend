namespace Finances.Domain.Entities;

/// <summary>
/// Obligation/debt owed by the user. The current balance and amount paid are NOT
/// stored here: they are derived from <see cref="Payments"/> plus the credit terms.
/// </summary>
public class Credit
{
    public int Id { get; set; }

    /// <summary>Friendly name, e.g. "Tarjeta Visa" or "Prestamo carro".</summary>
    public string Name { get; set; } = string.Empty;

    public CreditType Type { get; set; }

    /// <summary>How interest is charged (French vs simple/flat). Drives the calculation.</summary>
    public InterestModel InterestModel { get; set; } = InterestModel.CompoundFrench;

    /// <summary>How unearned interest is refunded on early payoff (mainly for flat credits).</summary>
    public PrepaymentRebateMethod PrepaymentRebateMethod { get; set; } = PrepaymentRebateMethod.Actuarial;

    /// <summary>What a partial prepayment does to the remaining plan (reduce term vs installment).</summary>
    public PrepaymentEffect PrepaymentEffect { get; set; } = PrepaymentEffect.ReduceTerm;

    /// <summary>Optional early-payoff penalty as a percentage of the prepaid amount (0 = none).</summary>
    public decimal PrepaymentPenaltyRate { get; set; }

    /// <summary>Original amount borrowed (capital).</summary>
    public decimal Principal { get; set; }

    /// <summary>Annual interest rate as a percentage, e.g. 2.3 means 2.3%.</summary>
    public decimal AnnualInterestRate { get; set; }

    /// <summary>Term (number of monthly installments).</summary>
    public int TermMonths { get; set; }

    /// <summary>Date the debt was acquired.</summary>
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    /// <summary>ISO currency code (optional), e.g. "USD".</summary>
    public string? Currency { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Owner of the credit (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;

    public ICollection<CreditPayment> Payments { get; set; } = new List<CreditPayment>();
}
