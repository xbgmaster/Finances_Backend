using MediatR;

namespace Finances.Application.Credits.Commands.DeleteCredit;

/// <summary>Deletes a credit (and its payments, via cascade) owned by the current user.</summary>
public record DeleteCreditCommand(int Id) : IRequest;
