using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Queries.GetCredits;

/// <summary>Query that returns all credits for the current user with headline figures.</summary>
public record GetCreditsQuery : IRequest<IReadOnlyList<CreditDto>>;
