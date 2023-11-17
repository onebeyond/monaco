#if filesSupport
#if (!disableAuth)
using Monaco.Template.Backend.Api.Auth;
#endif
using Monaco.Template.Backend.Application.Features.File.Commands;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.File.Queries;
using Monaco.Template.Backend.Common.Api.Application;
using MediatR;
#if (!disableAuth)
using Microsoft.AspNetCore.Authorization;
#endif
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Asp.Versioning;

namespace Monaco.Template.Backend.Api.Controllers
{
	[Route("api/v{apiVersion:apiVersion}/[controller]")]
    [ApiController]
    public class FilesController(IMediator mediator) : ControllerBase
    {
		[HttpPost]
#if (!disableAuth)
		[Authorize(Scopes.FilesWrite)]
#endif
		public Task<ActionResult<Guid>> Post([FromRoute] ApiVersion apiVersion, [FromForm] IFormFile file) =>
			mediator.ExecuteCommandAsync(new FileCreateCommand(file.OpenReadStream(), file.FileName, file.ContentType),
										  ModelState,
										  "api/v{0}/files/{1}",
										  apiVersion);

		[HttpGet("{id:guid}")]
#if (!disableAuth)
		[Authorize(Scopes.FilesRead)]
#endif
		public Task<ActionResult<FileDto>> Get(Guid id) =>
			mediator.ExecuteQueryAsync(new GetFileByIdQuery(id));

		[HttpGet("{id:guid}/Download")]
#if (!disableAuth)
		[Authorize(Scopes.FilesRead)]
#endif
		[ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Download(Guid id)
        {
            var result = await mediator.Send(new DownloadFileByIdQuery(id));

            if (result == null)
                return NotFound();

            return File(result.FileContent, result.ContentType, result.FileName);
        }

        [HttpDelete("{id:guid}")]
#if (!disableAuth)
		[Authorize(Scopes.FilesWrite)]
#endif
		public Task<IActionResult> Delete(Guid id) =>
			mediator.ExecuteCommandAsync(new FileDeleteCommand(id),
										  ModelState);
	}
}
#endif