using Finances.Application.Dtos;
using MediatR;

namespace Finances.Application.Credits.Queries.GetCredits;

/// <summary>Query that returns all credits for the current user with headline figures.
/// When <see cref="Currency"/> is set, only credits in that currency are returned.</summary>
public record GetCreditsQuery(string? Currency = null) : IRequest<IReadOnlyList<CreditDto>>;
