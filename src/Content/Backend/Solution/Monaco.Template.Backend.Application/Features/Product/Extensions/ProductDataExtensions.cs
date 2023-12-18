using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Product.Extensions;

public static class ProductDataExtensions
{
	internal static async Task<(Domain.Model.Company, Domain.Model.Image[])> GetProductData(this AppDbContext dbContext,
																					 Guid companyId,
																					 Guid[] pictures,
																					 CancellationToken cancellationToken)
	{
		var company = await dbContext.GetAsync<Domain.Model.Company>(companyId, cancellationToken);
		var pics = await dbContext.Set<Domain.Model.Image>()
								  .Include(x => x.Thumbnail)
								  .Where(x => pictures.Contains(x.Id))
								  .ToArrayAsync(cancellationToken);
		return (company, pics);
	}
}