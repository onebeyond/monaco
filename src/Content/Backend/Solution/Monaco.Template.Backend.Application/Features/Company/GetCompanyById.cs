using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class GetCompanyById
{
	public record Query(Guid Id) : QueryByIdBase<CompanyDto?>(Id);

	public sealed class Handler : IRequestHandler<Query, CompanyDto?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<CompanyDto?> Handle(Query request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Company>()
									   .AsNoTracking()
									   .Include(x => x.Address!.Country)
									   .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
			return item.Map(true);
		}
	}
}
