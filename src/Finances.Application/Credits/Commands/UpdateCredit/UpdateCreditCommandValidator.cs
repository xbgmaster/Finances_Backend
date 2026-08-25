using Finances.Domain.Entities;
using FluentValidation;

namespace Finances.Application.Credits.Commands.UpdateCredit;

public class UpdateCreditCommandValidator : AbstractValidator<UpdateCreditCommand>
{
    public UpdateCreditCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("El identificador del credito no es valido.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede superar 120 caracteres.");

        RuleFor(x => x.Type)
            .Must(t => Enum.TryParse<CreditType>(t, ignoreCase: true, out _))
            .WithMessage("El tipo de credito no es valido (CreditCard, InterestLoan, LibreInversion).");

        RuleFor(x => x.InterestModel)
            .Must(m => Enum.TryParse<InterestModel>(m, ignoreCase: true, out _))
            .WithMessage("El modelo de interes no es valido (CompoundFrench, SimpleFlat).");

        RuleFor(x => x.PrepaymentEffect)
            .Must(m => Enum.TryParse<PrepaymentEffect>(m, ignoreCase: true, out _))
            .WithMessage("El efecto de prepago no es valido (ReduceTerm, ReduceInstallment).");

        RuleFor(x => x.PrepaymentPenaltyRate)
            .InclusiveBetween(0, 100).WithMessage("La penalidad de prepago debe estar entre 0 y 100.");

        RuleFor(x => x.Principal)
            .GreaterThan(0).WithMessage("El valor del credito debe ser mayor que cero.");

        RuleFor(x => x.AnnualInterestRate)
            .GreaterThanOrEqualTo(0).WithMessage("La tasa de interes no puede ser negativa.")
            .LessThanOrEqualTo(1000).WithMessage("La tasa de interes anual no es valida.");

        RuleFor(x => x.TermMonths)
            .InclusiveBetween(1, 600).WithMessage("El plazo debe estar entre 1 y 600 meses.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria.");

        RuleFor(x => x.PaymentDueDay)
            .InclusiveBetween(1, 31).WithMessage("El dia de corte debe estar entre 1 y 31.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("La moneda es obligatoria.")
            .Length(3).WithMessage("El codigo de moneda debe tener 3 caracteres (ISO, ej. USD).");
    }
}
