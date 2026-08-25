using Finances.Domain.Entities;
using FluentValidation;

namespace Finances.Application.Credits.Commands.CreateCredit;

public class CreateCreditCommandValidator : AbstractValidator<CreateCreditCommand>
{
    public CreateCreditCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede superar 120 caracteres.");

        RuleFor(x => x.Type)
            .Must(t => Enum.TryParse<CreditType>(t, ignoreCase: true, out _))
            .WithMessage("El tipo de credito no es valido (CreditCard, InterestLoan, LibreInversion).");

        RuleFor(x => x.InterestModel)
            .Must(m => Enum.TryParse<InterestModel>(m, ignoreCase: true, out _))
            .WithMessage("El modelo de interes no es valido (CompoundFrench, SimpleFlat).");

        RuleFor(x => x.PrepaymentRebateMethod)
            .Must(m => Enum.TryParse<PrepaymentRebateMethod>(m, ignoreCase: true, out _))
            .WithMessage("El metodo de prepago no es valido (None, Actuarial, RuleOf78).");

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

        RuleFor(x => x.Currency)
            .MaximumLength(3).WithMessage("El codigo de moneda no puede superar 3 caracteres.");
    }
}
