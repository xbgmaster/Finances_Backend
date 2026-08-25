using Finances.Domain.Entities;
using FluentValidation;

namespace Finances.Application.Credits.Commands.UpdatePayment;

public class UpdatePaymentCommandValidator : AbstractValidator<UpdatePaymentCommand>
{
    public UpdatePaymentCommandValidator()
    {
        RuleFor(x => x.CreditId)
            .GreaterThan(0).WithMessage("El identificador del credito no es valido.");

        RuleFor(x => x.PaymentId)
            .GreaterThan(0).WithMessage("El identificador del pago no es valido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El valor del pago debe ser mayor que cero.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("La fecha del pago es obligatoria.");

        RuleFor(x => x.Type)
            .Must(t => Enum.TryParse<CreditPaymentType>(t, ignoreCase: true, out _))
            .WithMessage("El tipo de pago no es valido.");

        RuleFor(x => x.Effect)
            .Must(e => string.IsNullOrWhiteSpace(e) || Enum.TryParse<PrepaymentEffect>(e, ignoreCase: true, out _))
            .WithMessage("El efecto del abono no es valido.");

        RuleFor(x => x.Note)
            .MaximumLength(300).WithMessage("La nota no puede superar 300 caracteres.");
    }
}
