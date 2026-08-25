using FluentValidation;
using MediatR;
using AppValidationException = Finances.Application.Common.ValidationException;

namespace Finances.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically runs every registered
/// <see cref="IValidator{T}"/> for the current request before it reaches its
/// handler. This keeps validation out of the handlers (Single Responsibility)
/// and applies it as a cross-cutting concern to every command/query.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) =>
        _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var results = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = results
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count != 0)
            {
                // Translated into the Application layer's own exception so we can
                // reuse the existing GlobalExceptionHandler (HTTP 400 response).
                var message = string.Join(" ", failures.Select(f => f.ErrorMessage).Distinct());
                throw new AppValidationException(message);
            }
        }

        return await next();
    }
}
