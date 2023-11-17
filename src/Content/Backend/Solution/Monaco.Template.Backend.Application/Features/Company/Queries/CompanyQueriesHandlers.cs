using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company.Queries;

public sealed class CompanyQueriesHandlers(AppDbContext dbContext) : IRequestHandler<GetCompanyPageQuery, Page<CompanyDto>?>,
																	 IRequestHandler<GetCompanyByIdQuery, CompanyDto?>
{
	public async Task<Page<CompanyDto>?> Handle(GetCompanyPageQuery request, CancellationToken cancellationToken)
	{
		var query = dbContext.Set<Domain.Model.Company>()
							  .AsNoTracking();

		if (request.ExpandCountry)
			query = query.Include(x => x.Address!.Country);

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
		var item = await dbContext.Set<Domain.Model.Company>()
								   .AsNoTracking()
								   .Include(x => x.Address!.Country)
								   .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
		return item.Map(true);
	}
}