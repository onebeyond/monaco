#if includeFilesSupport
using DcslGs.Template.Api.Auth;
using DcslGs.Template.Application.Commands.File;
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Application.Queries.File;
using DcslGs.Template.Common.Api.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace DcslGs.Template.Api.Controllers
{
	[Route("api/v{apiVersion:apiVersion}/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FilesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [Authorize(Scopes.FilesWrite)]
        public Task<ActionResult<Guid>> Post(ApiVersion apiVersion, [FromForm] IFormFile file) =>
			_mediator.ExecuteCommandAsync(new FileCreateCommand(file.OpenReadStream(), file.FileName, file.ContentType),
										  ModelState,
										  "api/v1/files/{0}");

		[HttpGet("{id:guid}")]
        [Authorize(Scopes.FilesRead)]
        public Task<ActionResult<FileDto>> Get(Guid id) =>
			_mediator.ExecuteQueryAsync(new GetFileByIdQuery(id));

		[HttpGet("{id:guid}/Download")]
        [Authorize(Scopes.FilesRead)]
        [ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Download(Guid id)
        {
            var result = await _mediator.Send(new DownloadFileByIdQuery(id));

            if (result == null)
                return NotFound();

            return File(result.FileContent, result.ContentType, result.FileName);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Scopes.FilesWrite)]
        public Task<IActionResult> Delete(Guid id) =>
			_mediator.ExecuteCommandAsync(new FileDeleteCommand(id),
										  ModelState);
	}
}
#endif