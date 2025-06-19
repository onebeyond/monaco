using MediatR;
using Monaco.Template.Backend.Application.Features.Country.DTOs;
using Monaco.Template.Backend.Application.Features.Country.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Application.Queries.Extensions;

namespace Monaco.Template.Backend.Application.Features.Country;

public sealed class GetCountryById
{
	public sealed record Query(Guid Id) : QueryByIdBase<CountryDto?>(Id);

	internal sealed class Handler : IRequestHandler<Query, CountryDto?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public Task<CountryDto?> Handle(Query request, CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync<Domain.Model.Entities.Country, CountryDto>(_dbContext,
																				 x => x.Map(),
																				 cancellationToken);
	}
}
