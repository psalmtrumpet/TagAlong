using MediatR;
using TagAlong.Common.Results;

namespace TagAlong.Common.CQRS;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
