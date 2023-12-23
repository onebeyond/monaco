#if (!excludeFilesSupport)
#if (!disableAuth)
using Monaco.Template.Backend.Api.Auth;
#endif
using Monaco.Template.Backend.Application.Features.File;
using Monaco.Template.Backend.Common.Api.Application;
using MediatR;
#if (!disableAuth)
using Microsoft.AspNetCore.Authorization;
#endif
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Monaco.Template.Backend.Api.Controllers;

[Route("api/v{apiVersion:apiVersion}/[controller]")]
[ApiController]
public class FilesController : ControllerBase
{
	private readonly IMediator _mediator;

	public FilesController(IMediator mediator)
	{
		_mediator = mediator;
	}

	/// <summary>
	/// Uploads a new file that remains as temporal until it is referenced somewhere else in the app
	/// </summary>
	/// <param name="apiVersion"></param>
	/// <param name="file"></param>
	/// <returns></returns>
	[HttpPost]
	#if (!disableAuth)
	[Authorize(Scopes.FilesWrite)]
	#endif
	public Task<ActionResult<Guid>> Post([FromRoute] ApiVersion apiVersion, [FromForm] IFormFile file) =>
		_mediator.ExecuteCommandAsync(new CreateFile.Command(file.OpenReadStream(), file.FileName, file.ContentType),
									 ModelState,
									 "api/v{0}/files/{1}",
									 apiVersion);
}
#endif