using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Commands.CreateCredit;

/// <summary>
/// Command to register a new credit/obligation. The interest rate is an ANNUAL
/// percentage (e.g. 2.3 = 2.3%). Balances are never provided here; they are derived.
/// </summary>
public record CreateCreditCommand : IRequest<CreditDto>
{
    public string Name { get; init; } = string.Empty;

    /// <summary>One of: CreditCard, InterestLoan, LibreInversion.</summary>
    public string Type { get; init; } = "LibreInversion";

    /// <summary>Interest model: CompoundFrench (default) or SimpleFlat.</summary>
    public string InterestModel { get; init; } = "CompoundFrench";

    /// <summary>Rate conversion: EffectiveAnnual (default, % E.A.) or Nominal (annual/12).</summary>
    public string RateConvention { get; init; } = "EffectiveAnnual";

    /// <summary>Early-payoff rebate method: Actuarial (default), RuleOf78 or None.</summary>
    public string PrepaymentRebateMethod { get; init; } = "Actuarial";

    /// <summary>What a partial prepayment does: ReduceTerm (default) or ReduceInstallment.</summary>
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
