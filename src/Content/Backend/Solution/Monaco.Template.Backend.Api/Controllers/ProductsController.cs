using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Backend.Api.Auth;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Api.DTOs.Extensions;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.Application.Enums;
using Monaco.Template.Backend.Common.Domain.Model;
using System.Net;

namespace Monaco.Template.Backend.Api.Controllers;

[Route("api/v{apiVersion:apiVersion}/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
	private readonly IMediator _mediator;

	public ProductsController(IMediator mediator)
	{
		_mediator = mediator;
	}

	/// <summary>
	/// Gets a page of products
	/// </summary>
	/// <returns></returns>
	[HttpGet]
	[AllowAnonymous]
	public Task<ActionResult<Page<ProductDto>>> Get() =>
		_mediator.ExecuteQueryAsync(new GetProductPage.Query(Request.Query));

	/// <summary>
	/// Gets a product by Id
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	[HttpGet("{id:guid}")]
	[AllowAnonymous]
	public Task<ActionResult<ProductDto?>> Get(Guid id) =>
		_mediator.ExecuteQueryAsync(new GetProductById.Query(id));

	/// <summary>
	/// Creates a new product
	/// </summary>
	/// <param name="apiVersion"></param>
	/// <param name="dto"></param>
	/// <returns></returns>
	[HttpPost]
	#if (!disableAuth)
	[Authorize(Scopes.ProductsWrite)]
	#endif
	public Task<ActionResult<Guid>> Post([FromRoute] ApiVersion apiVersion, [FromBody] ProductCreateEditDto dto) =>
		_mediator.ExecuteCommandAsync(dto.Map(),
									  ModelState,
									  "api/v{0}/Products/{1}",
									  apiVersion);

	/// <summary>
	/// Edits an existing product
	/// </summary>
	/// <param name="id"></param>
	/// <param name="dto"></param>
	/// <returns></returns>
	[HttpPut("{id:guid}")]
	#if (!disableAuth)
	[Authorize(Scopes.ProductsWrite)]
	#endif
	public Task<IActionResult> Put(Guid id, [FromBody] ProductCreateEditDto dto) =>
		_mediator.ExecuteCommandAsync(dto.Map(id),
									  ModelState,
									  ResponseType.NoContent);

	/// <summary>
	/// Deletes a product
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	[HttpDelete("{id:guid}")]
	#if (!disableAuth)
	[Authorize(Scopes.ProductsWrite)]
	#endif
	public Task<IActionResult> Delete(Guid id) =>
		_mediator.ExecuteCommandAsync(new DeleteProduct.Command(id),
									  ModelState);

	/// <summary>
	/// Downloads a picture from a product
	/// </summary>
	/// <param name="productId"></param>
	/// <param name="pictureId"></param>
	/// <returns></returns>
	[HttpGet("{productId:guid}/Pictures/{pictureId:guid}")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
	[ProducesResponseType((int)HttpStatusCode.NotFound)]
	public Task<IActionResult> Download(Guid productId, Guid pictureId) =>
		_mediator.ExecuteFileDownloadAsync(new DownloadProductPicture.Query(productId, pictureId, Request.Query));
}
