using MediatR;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Application.Queries.Extensions;

namespace Monaco.Template.Backend.Application.Features.Country;

public sealed class GetCountryById
{
	public record Query(Guid Id) : QueryByIdBase<CountryDto?>(Id);

	public sealed class Handler : IRequestHandler<Query, CountryDto?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public Task<CountryDto?> Handle(Query request, CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync<Domain.Model.Country, CountryDto>(_dbContext,
																		x => x.Map(),
																		cancellationToken);
	}
}
