using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Features.Company.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class GetCompanyById
{
	public sealed record Query(Guid Id) : QueryByIdBase<CompanyDto?>(Id);

	internal sealed class Handler : IRequestHandler<Query, CompanyDto?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<CompanyDto?> Handle(Query request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Entities.Company>()
									   .AsNoTracking()
									   .Include(x => x.Address!.Country)
									   .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
			return item.Map(true);
		}
	}
}
