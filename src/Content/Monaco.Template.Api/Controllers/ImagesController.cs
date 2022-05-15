#if includeFilesSupport
using Monaco.Template.Api.Auth;
using Monaco.Template.Application.DTOs;
using Monaco.Template.Application.Features.File.Queries;
using Monaco.Template.Application.Features.Image.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Monaco.Template.Common.Api.Application;

namespace Monaco.Template.Api.Controllers
{
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
        [Authorize(Scopes.FilesRead)]
        public Task<ActionResult<ImageDto>> Get(Guid id) =>
			_mediator.ExecuteQueryAsync(new GetImageByIdQuery(id));

		[HttpGet("{id:guid}/Thumbnail")]
        [Authorize(Scopes.FilesRead)]
        public Task<ActionResult<ImageDto>> GetThumbnail(Guid id) =>
			_mediator.ExecuteQueryAsync(new GetThumbnailByImageIdQuery(id));

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

        [HttpGet("{id:guid}/Thumbnail/Download")]
        [Authorize(Scopes.FilesRead)]
        [ProducesResponseType(typeof(FileContentResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DownloadThumbnail(Guid id)
        {
            var result = await _mediator.Send(new DownloadThumbnailByImageIdQuery(id));

            if (result == null)
                return NotFound();

            return File(result.FileContent, result.ContentType, result.FileName);
        }
    }
}
#endif