using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Application.DTOs;
using Monaco.Template.Application.DTOs.Extensions;
using Monaco.Template.Application.Infrastructure.Context;
using Monaco.Template.Common.Domain.Model;
using Monaco.Template.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Application.Features.Company.Queries;

public sealed class CompanyQueriesHandlers : IRequestHandler<GetCompanyPageQuery, Page<CompanyDto>?>,
											 IRequestHandler<GetCompanyByIdQuery, CompanyDto?>
{
	private readonly AppDbContext _dbContext;

	public CompanyQueriesHandlers(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<Page<CompanyDto>?> Handle(GetCompanyPageQuery request, CancellationToken cancellationToken)
	{
		var query = _dbContext.Set<Domain.Model.Company>()
							  .AsNoTracking();

		if (request.ExpandCountry)
			query = query.Include(x => x.Country);

		var page = await query.ApplyFilter(request.QueryString, CompanyExtensions.GetMappedFields())
							  .ApplySort(request.Sort, nameof(CompanyDto.Name), CompanyExtensions.GetMappedFields())
							  .ToPageAsync(request.Offset,
										   request.Limit,
										   x => x.Map(request.ExpandCountry)!,
										   cancellationToken);
		return page;
	}

	public async Task<CompanyDto?> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
	{
		var item = await _dbContext.Set<Domain.Model.Company>()
								   .AsNoTracking()
								   .Include(x => x.Country)
								   .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
		return item.Map(true);
	}
}