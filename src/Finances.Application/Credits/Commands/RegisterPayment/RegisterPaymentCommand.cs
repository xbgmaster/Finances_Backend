using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Commands.RegisterPayment;

/// <summary>
/// Records a payment against a credit and returns the recalculated summary.
/// <see cref="CreditId"/> is set by the controller from the route.
/// </summary>
public record RegisterPaymentCommand : IRequest<CreditSummaryDto>
{
    public int CreditId { get; init; }
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
    public string? Note { get; init; }
}
