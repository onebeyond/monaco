using Monaco.Template.Common.Application.Queries.Extensions;
using Monaco.Template.Application.Infrastructure.Context;
using MediatR;
using Monaco.Template.Application.DTOs;
using Monaco.Template.Application.DTOs.Extensions;

namespace Monaco.Template.Application.Features.Country.Queries;

public sealed class CountryQueriesHandlers : IRequestHandler<GetCountryListQuery, List<CountryDto>>,
											 IRequestHandler<GetCountryByIdQuery, CountryDto?>
{
	private readonly AppDbContext _dbContext;

	public CountryQueriesHandlers(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public Task<List<CountryDto>> Handle(GetCountryListQuery request, CancellationToken cancellationToken) =>
		request.ExecuteQueryAsync(_dbContext,
								  x => x.Map()!,
								  nameof(CountryDto.Name),
								  CountryExtensions.GetMappedFields(),
								  cancellationToken);

	public Task<CountryDto?> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken) =>
		request.ExecuteQueryAsync<Domain.Model.Country, CountryDto>(_dbContext,
																	x => x.Map(),
																	cancellationToken);
}