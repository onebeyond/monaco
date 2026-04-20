using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Api.DTOs.Extensions;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Features.Product.DTOs;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.MinimalApi;
using Monaco.Template.Backend.Common.Domain.Model;
#if (auth)
using Monaco.Template.Backend.Api.Auth;
#endif

namespace Monaco.Template.Backend.Api.Endpoints;

internal static class Products
{
	extension(IEndpointRouteBuilder builder)
	{
		public IEndpointRouteBuilder AddProducts(ApiVersionSet versionSet)
		{
			var products = builder.CreateApiGroupBuilder(versionSet,
														 "Products");

			products.MapGet("",
							Task<Results<Ok<Page<ProductDto>>, NotFound>> ([FromServices] ISender sender,
																		   HttpRequest request,
																		   CancellationToken cancellationToken) =>
								sender.ExecuteQueryAsync(new GetProductPage.Query(request.Query),
														 cancellationToken),
							"GetProducts",
#if (!auth)
							"Gets a page of products");
#else
							"Gets a page of products")
					.AllowAnonymous();
#endif

			products.MapGet("{id:guid}",
							Task<Results<Ok<ProductDto?>, NotFound>> ([FromServices] ISender sender,
																	  [FromRoute] Guid id,
																	  CancellationToken cancellationToken) =>
								sender.ExecuteQueryAsync(new GetProductById.Query(id),
														 cancellationToken),
							"GetProduct",
#if (!auth)
							"Gets a product by Id");
#else
							"Gets a product by Id")
					.AllowAnonymous();
#endif

			products.MapPost("",
							 Task<Results<Created<CreatedResponse>, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ([FromServices] ISender sender,
																															   [FromBody] ProductCreateEditDto dto,
																															   HttpContext context,
																															   CancellationToken cancellationToken) =>
								 sender.ExecuteCommandCreatedAsync(dto.Map(),
																   "api/v{0}/Products/{1}",
																   [context.GetRequestedApiVersion()!],
																   cancellationToken),
							 "CreateProduct",
#if (!auth)
							 "Create a new product");
#else
							 "Create a new product")
					.RequireAuthorization(Scopes.ProductsWrite);
#endif

			products.MapPut("{id:guid}",
							Task<Results<NoContent, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ([FromServices] ISender sender,
																											   [FromRoute] Guid id,
																											   [FromBody] ProductCreateEditDto dto,
																											   CancellationToken cancellationToken) =>
								sender.ExecuteCommandNoContentAsync(dto.Map(id),
																	cancellationToken),
							"EditProduct",
#if (!auth)
							"Edit an existing product by Id");
#else
							"Edit an existing product by Id")
					.RequireAuthorization(Scopes.ProductsWrite);
#endif

			products.MapDelete("{id:guid}",
							   Task<Results<Ok, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ([FromServices] ISender sender,
																										   [FromRoute] Guid id,
																										   CancellationToken cancellationToken) =>
								   sender.ExecuteCommandOkAsync(new DeleteProduct.Command(id),
																cancellationToken),
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
																		   HttpRequest request,
																		   CancellationToken cancellationToken) =>
								sender.ExecuteFileDownloadAsync(new DownloadProductPicture.Query(productId,
																								 pictureId,
																								 request.Query),
																cancellationToken),
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
}