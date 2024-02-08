using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
#if (auth)
using Monaco.Template.Backend.Api.Auth;
#endif
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Api.DTOs.Extensions;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.MinimalApi;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Api.Endpoints;

public static class Products
{
	public static IEndpointRouteBuilder AddProducts(this IEndpointRouteBuilder builder, ApiVersionSet versionSet)
	{
		var products = builder.CreateApiGroupBuilder(versionSet, "Products");

		products.MapGet("",
						Task<Results<Ok<Page<ProductDto>>, NotFound>> ([FromServices] ISender sender,
																	   HttpRequest request) =>
							sender.ExecuteQueryAsync(new GetProductPage.Query(request.Query)),
						"GetProducts",
#if (!auth)
						"Gets a page of products");
#else
						"Gets a page of products")
				.AllowAnonymous();
#endif

		products.MapGet("{id:guid}",
						Task<Results<Ok<ProductDto?>, NotFound>> ([FromServices] ISender sender,
																  [FromRoute] Guid id) =>
							sender.ExecuteQueryAsync(new GetProductById.Query(id)),
						"GetProduct",
#if (!auth)
						"Gets a product by Id");
#else
						"Gets a product by Id")
				.AllowAnonymous();
#endif

		products.MapPost("",
						 Task<Results<Created<Guid>, NotFound, ValidationProblem>> ([FromServices] ISender sender,
																					[FromBody] ProductCreateEditDto dto,
																					HttpContext context) =>
							 sender.ExecuteCommandAsync(dto.Map(),
														"api/v{0}/Products/{1}",
														context.GetRequestedApiVersion()!),
						 "CreateProduct",
#if (!auth)
						 "Create a new product");
#else
						 "Create a new product")
				.RequireAuthorization(Scopes.ProductsWrite);
#endif

		products.MapPut("{id:guid}",
						Task<Results<NoContent, NotFound, ValidationProblem>> ([FromServices] ISender sender,
																			   [FromRoute] Guid id,
																			   [FromBody] ProductCreateEditDto dto) =>
							sender.ExecuteCommandEditAsync(dto.Map(id)),
						"EditProduct",
#if (!auth)
						"Edit an existing product by Id");
#else
						"Edit an existing product by Id")
				.RequireAuthorization(Scopes.ProductsWrite);
#endif

		products.MapDelete("{id:guid}",
						   Task<Results<Ok, NotFound, ValidationProblem>> ([FromServices] ISender sender,
																		   [FromRoute] Guid id) =>
							   sender.ExecuteCommandDeleteAsync(new DeleteProduct.Command(id)),
						   "DeleteProduct",
#if (!auth)
						   "Delete an existing product by Id");
#else
						   "Delete an existing product by Id")
				.RequireAuthorization(Scopes.ProductsWrite);
#endif

		products.MapGet("{productId:guid}/Pictures/{pictureId:guid}",
						Task<Results<FileStreamHttpResult, NotFound>> ([FromServices] ISender sender,
																	   [FromRoute] Guid productId,
																	   [FromRoute] Guid pictureId,
																	   HttpRequest request) =>
							sender.ExecuteFileDownloadAsync(new DownloadProductPicture.Query(productId,
																							 pictureId,
																							 request.Query)),
						"DownloadProductPicture",
						"Download a picture from a product by Id")
#if (!auth)
				.Produces(StatusCodes.Status200OK);
#else
				.Produces(StatusCodes.Status200OK)
				.AllowAnonymous();
#endif

		return builder;
	}
}