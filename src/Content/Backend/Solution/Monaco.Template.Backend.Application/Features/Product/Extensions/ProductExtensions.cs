using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Features.Company.Extensions;
using Monaco.Template.Backend.Application.Features.File.Extensions;
using Monaco.Template.Backend.Application.Features.Product.DTOs;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Domain.Model.Entities;
#if massTransitIntegration
using Monaco.Template.Backend.Messages.V1;
#endif

namespace Monaco.Template.Backend.Application.Features.Product.Extensions;

public static class ProductExtensions
{
	extension(Domain.Model.Entities.Product value)
	{
		public ProductDto Map(bool expandCompany = false,
							  bool expandPictures = false,
							  bool expandDefaultPicture = false) =>
			new(value.Id,
				value.Title,
				value.Description,
				value.Price,
				value.CompanyId,
				expandCompany
					? value.Company
						   .Map()
					: null,
				expandPictures
					? [..value.Pictures.Select(x => x.Map()!)]
					: null,
				value.DefaultPictureId,
				expandDefaultPicture
					? value.DefaultPicture
						   .Map()
					: null);
	}
#if (massTransitIntegration)
	
	extension(Domain.Model.Entities.Product item)
	{
		internal ProductCreated MapMessage() =>
			new(item.Id,
				item.Title,
				item.Description,
				item.Price,
				item.CompanyId);
	}
#endif

	extension(AppDbContext dbContext)
	{
		internal async Task<(Domain.Model.Entities.Company company , Image[] pics)> GetProductData(Guid companyId,
																								   Guid[] pictures,
																								   CancellationToken cancellationToken)
		{
			var company = await dbContext.Set<Domain.Model.Entities.Company>()
										 .SingleAsync(x => x.Id == companyId, cancellationToken);
			var pics = await dbContext.Set<Image>()
									  .Include(x => x.Thumbnail)
									  .Where(x => ((IEnumerable<Guid>)pictures).Contains(x.Id))
									  .ToArrayAsync(cancellationToken);
			return (company, pics);
		}
	}
}