using FluentValidation;
using MediatR;
using TagAlong.Common.Results;

namespace TagAlong.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            var error = Error.Validation(
                "Validation.Failed",
                string.Join("; ", failures.Select(f => f.ErrorMessage)));

            return CreateValidationResult<TResponse>(error);
        }

        return await next();
    }

    private static TResponse CreateValidationResult<TResult>(Error error)
    {
        if (typeof(TResult) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        var resultType = typeof(TResult).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, new[] { typeof(Error) })!
            .MakeGenericMethod(resultType);

        return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
    }
}
