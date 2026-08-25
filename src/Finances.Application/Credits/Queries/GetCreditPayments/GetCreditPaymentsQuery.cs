using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Queries.GetCreditPayments;

/// <summary>Query that lists the payments recorded against a credit (most recent first).</summary>
public record GetCreditPaymentsQuery(int CreditId) : IRequest<IReadOnlyList<CreditPaymentDto>>;
