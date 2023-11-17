using MediatR;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries.Extensions;

namespace Monaco.Template.Backend.Application.Features.Country.Queries;

public sealed class CountryQueriesHandlers(AppDbContext dbContext) : IRequestHandler<GetCountryListQuery, List<CountryDto>>,
																	 IRequestHandler<GetCountryByIdQuery, CountryDto?>
{
	public Task<List<CountryDto>> Handle(GetCountryListQuery request, CancellationToken cancellationToken) =>
		request.ExecuteQueryAsync(dbContext,
								  x => x.Map()!,
								  nameof(CountryDto.Name),
								  CountryExtensions.GetMappedFields(),
								  cancellationToken);

	public Task<CountryDto?> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken) =>
		request.ExecuteQueryAsync<Domain.Model.Country, CountryDto>(dbContext,
																	x => x.Map(),
																	cancellationToken);
}