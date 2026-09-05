using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Commands.UpdateCredit;

/// <summary>
/// Updates the terms of an existing credit. Balances/progress are never provided; they are
/// re-derived from the (unchanged) payment ledger against the new terms. Returns the
/// recalculated summary, or null when the credit does not exist for the current user.
/// </summary>
public record UpdateCreditCommand : IRequest<CreditSummaryDto?>
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>One of: CreditCard, InterestLoan, LibreInversion.</summary>
    public string Type { get; init; } = "LibreInversion";

    /// <summary>Interest model: CompoundFrench or SimpleFlat.</summary>
    public string InterestModel { get; init; } = "CompoundFrench";

    /// <summary>Rate conversion: EffectiveAnnual (% E.A.) or Nominal (annual/12). Defaults to
    /// Nominal so an old client that omits it does not silently change an existing credit.</summary>
    public string RateConvention { get; init; } = "Nominal";

    /// <summary>What a partial prepayment does: ReduceTerm or ReduceInstallment.</summary>
    public string PrepaymentEffect { get; init; } = "ReduceTerm";

    /// <summary>Optional early-payoff penalty as a percentage of the prepaid amount (0 = none).</summary>
    public decimal PrepaymentPenaltyRate { get; init; }

    /// <summary>Optional monthly life-insurance premium as a % of the outstanding balance (0 = none).</summary>
    public decimal MonthlyInsuranceRate { get; init; }

    /// <summary>Optional fixed monthly charge for commissions/other insurances (0 = none).</summary>
    public decimal MonthlyFixedFee { get; init; }

    public decimal Principal { get; init; }
    public decimal AnnualInterestRate { get; init; }
    public int TermMonths { get; init; }
    public DateTime StartDate { get; init; }

    /// <summary>Day of the month (1-31) the installment/statement is due.</summary>
    public int PaymentDueDay { get; init; }

    public string? Currency { get; init; }
}
