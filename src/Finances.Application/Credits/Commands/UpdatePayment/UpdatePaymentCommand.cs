using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Commands.UpdatePayment;

/// <summary>
/// Edits an existing payment and returns the recalculated summary.
/// <see cref="CreditId"/> and <see cref="PaymentId"/> are set by the controller from the route.
/// </summary>
public record UpdatePaymentCommand : IRequest<CreditSummaryDto>
{
    public int CreditId { get; init; }
    public int PaymentId { get; init; }

    /// <summary>Amount in the credit's own currency (drives the amortization and the mirrored expense).</summary>
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
    public string? Note { get; init; }

    /// <summary>"Installment" (regular) or "PrincipalPrepayment" (abono a capital).</summary>
    public string Type { get; init; } = "Installment";

    /// <summary>For a prepayment: "ReduceTerm" or "ReduceInstallment". Null uses the credit default.</summary>
    public string? Effect { get; init; }
}
