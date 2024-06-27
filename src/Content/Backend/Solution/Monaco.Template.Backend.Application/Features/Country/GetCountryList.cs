using MediatR;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Application.Queries.Extensions;

namespace Monaco.Template.Backend.Application.Features.Country;

public sealed class GetCountryList
{
	public record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : CachedQueryBase<List<CountryDto>>(QueryString)
	{
		public override string CacheKey => $"get-country-list-{GetQueryStringHashCode()}";
	}

	public sealed class Handler : IRequestHandler<Query, List<CountryDto>>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public Task<List<CountryDto>> Handle(Query request, CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync(_dbContext,
									  x => x.Map()!,
									  nameof(CountryDto.Name),
									  CountryExtensions.GetMappedFields(),
									  cancellationToken);
	}
}
