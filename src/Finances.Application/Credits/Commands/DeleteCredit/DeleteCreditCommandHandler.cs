using Finances.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Credits.Commands.DeleteCredit;

public class DeleteCreditCommandHandler : IRequestHandler<DeleteCreditCommand>
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public DeleteCreditCommandHandler(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task Handle(DeleteCreditCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var credit = await _db.Credits
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("El credito no existe.");

        _db.Credits.Remove(credit);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
