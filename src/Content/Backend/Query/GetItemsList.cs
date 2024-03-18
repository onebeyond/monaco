using MediatR;
using Microsoft.Extensions.Primitives;
using ProjectName.DTOs;
using ProjectName.DTOs.Extensions;
using ProjectName.Infrastructure.Context;
using CommonSolutionName.Common.Application.Queries;
using CommonSolutionName.Common.Application.Queries.Extensions;

namespace ProjectName.Features.MyFeature;

public sealed class GetItemsList
{
	public record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryBase<List<ItemDto>>(QueryString);

	public sealed class Handler : IRequestHandler<Query, List<ItemDto>>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public Task<List<ItemDto>> Handle(Query request, CancellationToken cancellationToken) =>
			throw new NotImplementedException();
	}
}
