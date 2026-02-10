using MediatR;
using TagAlong.Common.Results;

namespace TagAlong.Common.CQRS;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
