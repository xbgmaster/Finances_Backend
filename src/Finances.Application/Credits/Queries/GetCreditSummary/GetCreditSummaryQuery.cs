using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Queries.GetCreditSummary;

/// <summary>Query for the full "smart summary" of a single credit as of today.</summary>
public record GetCreditSummaryQuery(int Id) : IRequest<CreditSummaryDto?>;
