using System.Net;
using DcslGs.Template.Api.Auth;
using DcslGs.Template.Application.Commands.File;
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Application.Queries.File;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> Post(ApiVersion apiVersion, [FromForm] IFormFile file)
        {
            var cmd = new FileCreateCommand(file.OpenReadStream(), file.FileName, file.ContentType);
            var result = await _mediator.Send(cmd);

            if (result.ValidationResult.IsValid)
                return Created($"api/v{apiVersion}/files/{result.Result}", result.Result);

            result.ValidationResult.AddToModelState(ModelState, null);
            return BadRequest(ModelState);
        }

        [HttpGet("{id}")]
        [Authorize(Scopes.FilesRead)]
        public async Task<ActionResult<FileDto>> Get(Guid id)
        {
            var result = await _mediator.Send(new GetFileByIdQuery(id));

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpGet("{id}/Download")]
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

        [HttpDelete("{id}")]
        [Authorize(Scopes.FilesWrite)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _mediator.Send(new FileDeleteCommand(id));

            if (result.ItemNotFound)
                return NotFound();

            if (result.ValidationResult.IsValid)
                return Ok();

            result.ValidationResult.AddToModelState(ModelState, null);
            return BadRequest(ModelState);
        }
    }
}
