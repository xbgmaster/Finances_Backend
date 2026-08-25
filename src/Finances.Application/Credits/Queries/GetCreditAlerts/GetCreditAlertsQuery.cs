using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Queries.GetCreditAlerts;

/// <summary>
/// Returns the credits that currently need attention (overdue or due soon) for the
/// signed-in user. Powers the notification bell and the dashboard alert banner.
/// </summary>
public record GetCreditAlertsQuery : IRequest<CreditAlertsDto>;
