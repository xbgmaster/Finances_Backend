using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Queries.GetAmortizationSchedule;

/// <summary>Query for the full month-by-month amortization table of a credit.</summary>
public record GetAmortizationScheduleQuery(int Id) : IRequest<CreditScheduleDto?>;
