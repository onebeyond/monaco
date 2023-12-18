using Monaco.Template.Backend.Application.Features.Product;

namespace Monaco.Template.Backend.Api.DTOs.Extensions;

public static class ProductExtensions
{
	public static CreateProduct.Command Map(this ProductCreateEditDto value) =>
		new(value.Title,
			value.Description,
			value.Price,
			value.CompanyId,
			value.Pictures,
			value.DefaultPictureId);

	public static EditProduct.Command Map(this ProductCreateEditDto value, Guid id) =>
		new(id,
			value.Title,
			value.Description,
			value.Price,
			value.CompanyId,
			value.Pictures,
			value.DefaultPictureId);
}