using MediatR;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Country.DTOs;
using Monaco.Template.Backend.Application.Features.Country.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Application.Queries.Extensions;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Application.Features.Country;

public sealed class GetCountryList
{
	public sealed record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) : QueryBase<List<CountryDto>, Domain.Model.Entities.Country>(QueryParams)
	{
		public override Dictionary<string, Expression<Func<Domain.Model.Entities.Country, object>>> GetFilteringMappedFields() =>
			new()
			{
				[nameof(CountryDto.Id)] = x => x.Id,
				[nameof(CountryDto.Name)] = x => x.Name
			};
	}

	internal sealed class Handler : IRequestHandler<Query, QueryResult<List<CountryDto>>>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public Task<QueryResult<List<CountryDto>>> Handle(Query request, CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync(_dbContext,
									  x => x.Map(),
									  nameof(CountryDto.Name),
									  request.GetFilteringMappedFields(),
									  cancellationToken);
	}
}
