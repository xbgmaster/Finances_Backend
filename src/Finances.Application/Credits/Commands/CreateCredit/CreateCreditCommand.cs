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

    public decimal Principal { get; init; }
    public decimal AnnualInterestRate { get; init; }
    public int TermMonths { get; init; }
    public DateTime StartDate { get; init; }
    public string? Currency { get; init; }
}
