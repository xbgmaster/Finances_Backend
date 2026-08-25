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

    /// <summary>Early-payoff rebate method: Actuarial (default), RuleOf78 or None.</summary>
    public string PrepaymentRebateMethod { get; init; } = "Actuarial";

    /// <summary>What a partial prepayment does: ReduceTerm (default) or ReduceInstallment.</summary>
    public string PrepaymentEffect { get; init; } = "ReduceTerm";

    /// <summary>Optional early-payoff penalty as a percentage of the prepaid amount (0 = none).</summary>
    public decimal PrepaymentPenaltyRate { get; init; }

    public decimal Principal { get; init; }
    public decimal AnnualInterestRate { get; init; }
    public int TermMonths { get; init; }
    public DateTime StartDate { get; init; }
    public string? Currency { get; init; }
}
