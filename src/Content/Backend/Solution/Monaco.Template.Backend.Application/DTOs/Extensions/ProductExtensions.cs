using LinqKit;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Domain.Model;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Application.DTOs.Extensions;

public static class ProductExtensions
{
	public static ProductDto? Map(this Product? value,
								  bool expandCompany = false,
								  bool expandPictures = false,
								  bool expandDefaultPicture = false) =>
		value is null
			? null
			: new(value.Id,
				  value.Title,
				  value.Description,
				  value.Price,
				  value.CompanyId,
				  expandCompany
					  ? value.Company
							 .Map()
					  : null,
				  expandPictures
					  ? value.Pictures
							 .Select(x => x.Map()!)
							 .ToArray()
					  : null,
				  value.DefaultPictureId,
				  expandDefaultPicture
					  ? value.DefaultPicture
							 .Map()
					  : null);

	public static Product Map(this CreateProduct.Command value, Company company, Image[] pictures, Image defaultPicture)
	{
		var item = new Product(value.Title,
							   value.Description,
							   value.Price,
							   company);
		foreach (var picture in pictures)
			item.AddPicture(picture, picture == defaultPicture);

		return item;
	}

	public static (Image[]newPics, Image[] deletedPics) Map(this EditProduct.Command value, Product item, Company company, Image[] pictures, Image defaultPicture)
	{
		item.Update(value.Title,
					value.Description,
					value.Price,
					company);

		var deletedPics = item.Pictures
							  .Where(p => !pictures.Contains(p))
							  .ToArray();
		deletedPics.ForEach(item.RemovePicture);

		var newPics = pictures.Where(p => p.IsTemp)
							  .ToArray();

		pictures.ForEach(p => item.AddPicture(p, p == defaultPicture));

		return (newPics, deletedPics);
	}

	public static Dictionary<string, Expression<Func<Product, object>>> GetMappedFields() =>
		new()
		{
			[nameof(ProductDto.Id)] = x => x.Id,
			[nameof(ProductDto.Title)] = x => x.Title,
			[nameof(ProductDto.Description)] = x => x.Description,
			[nameof(ProductDto.Price)] = x => x.Price,
			[nameof(ProductDto.CompanyId)] = x => x.CompanyId,
			[$"{nameof(ProductDto.Company)}.{nameof(CompanyDto.Name)}"] = x => x.Company.Name,
			[nameof(ProductDto.DefaultPictureId)] = x => x.DefaultPictureId
		};
}