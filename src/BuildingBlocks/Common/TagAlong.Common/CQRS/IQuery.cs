using MediatR;
using TagAlong.Common.Results;

namespace TagAlong.Common.CQRS;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
