#if filesSupport
#if (!disableAuth)
using Monaco.Template.Backend.Api.Auth;
#endif
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.File;
using Monaco.Template.Backend.Application.Features.Image;
using MediatR;
#if (!disableAuth)
using Microsoft.AspNetCore.Authorization;
#endif
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Monaco.Template.Backend.Common.Api.Application;

namespace Monaco.Template.Backend.Api.Controllers;

[Route("api/v{apiVersion:apiVersion}/[controller]")]
[ApiController]
public class ImagesController : ControllerBase
{
	private readonly IMediator _mediator;

	public ImagesController(IMediator mediator)
	{
		_mediator = mediator;
	}

	[HttpGet("{id:guid}")]
	#if (!disableAuth)
	[Authorize(Scopes.FilesRead)]
	#endif
	public Task<ActionResult<ImageDto>> Get(Guid id) =>
		_mediator.ExecuteQueryAsync(new GetImageById.Query(id));

	[HttpGet("{id:guid}/Thumbnail")]
	#if (!disableAuth)
	[Authorize(Scopes.FilesRead)]
	#endif
	public Task<ActionResult<ImageDto>> GetThumbnail(Guid id) =>
		_mediator.ExecuteQueryAsync(new GetThumbnailByImageId.Query(id));

	[HttpGet("{id:guid}/Download")]
	#if (!disableAuth)
	[Authorize(Scopes.FilesRead)]
	#endif
	[ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
	[ProducesResponseType((int)HttpStatusCode.NotFound)]
	public async Task<IActionResult> Download(Guid id)
	{
		var result = await _mediator.Send(new DownloadFileById.Query(id));

		if (result is null)
			return NotFound();

		return File(result.FileContent, result.ContentType, result.FileName);
	}

	[HttpGet("{id:guid}/Thumbnail/Download")]
	#if (!disableAuth)
	[Authorize(Scopes.FilesRead)]
	#endif
	[ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
	[ProducesResponseType((int)HttpStatusCode.NotFound)]
	public async Task<IActionResult> DownloadThumbnail(Guid id)
	{
		var result = await _mediator.Send(new DownloadThumbnailByImageId.Query(id));

		if (result is null)
			return NotFound();

		return File(result.FileContent, result.ContentType, result.FileName);
	}
}
#endif