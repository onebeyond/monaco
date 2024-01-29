using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
#if (massTransitIntegration)
using Monaco.Template.Backend.Messages;
#endif

namespace Monaco.Template.Backend.Application.Features.Product.Extensions;

public static class ProductDataExtensions
{
	internal static async Task<(Domain.Model.Company, Domain.Model.Image[])> GetProductData(this AppDbContext dbContext,
																							Guid companyId,
																							Guid[] pictures,
																							CancellationToken cancellationToken)
	{
		var company = await dbContext.Set<Domain.Model.Company>()
									 .Include(x => x.Products)
									 .SingleAsync(x => x.Id == companyId, cancellationToken);
		var pics = await dbContext.Set<Domain.Model.Image>()
								  .Include(x => x.Thumbnail)
								  .Where(x => pictures.Contains(x.Id))
								  .ToArrayAsync(cancellationToken);
		return (company, pics);
	}
#if (massTransitIntegration)
	
	internal static ProductCreated MapMessage(this Domain.Model.Product item) =>
		new(item.Id,
			item.Title,
			item.Description,
			item.Price,
			item.CompanyId);
#endif
}