using MediatR;

namespace Finances.Application.Credits.Commands.DeletePayment;

/// <summary>Deletes a single payment from a credit owned by the current user.</summary>
public record DeletePaymentCommand(int CreditId, int PaymentId) : IRequest;
