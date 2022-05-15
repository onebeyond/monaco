using Monaco.Template.Api.DTOs.Extensions;
using Monaco.Template.Application.DTOs;
using Monaco.Template.Application.Features.Company.Commands;
using Monaco.Template.Application.Features.Company.Queries;
using Monaco.Template.Common.Api.Application;
using Monaco.Template.Common.Api.Application.Enums;
using Monaco.Template.Common.Domain.Model;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Api.Auth;
using Monaco.Template.Api.DTOs;

namespace Monaco.Template.Api.Controllers;

[Route("api/v{apiVersion:apiVersion}/[controller]")]
[ApiController]
public class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;
	
	public CompaniesController(IMediator mediator)
	{
		_mediator = mediator;
    }

    [HttpGet]
    [Authorize(Scopes.CompaniesRead)]
    public Task<ActionResult<Page<CompanyDto>>> Get() =>
		_mediator.ExecuteQueryAsync(new GetCompanyPageQuery(Request.Query));

	[HttpGet("{id:guid}")]
    [Authorize(Scopes.CompaniesRead)]
    public Task<ActionResult<CompanyDto?>> Get(Guid id) => 
		_mediator.ExecuteQueryAsync(new GetCompanyByIdQuery(id));

	[HttpPost]
    [Authorize(Scopes.CompaniesWrite)]
    public Task<ActionResult<Guid>> Post([FromRoute] ApiVersion apiVersion, [FromBody] CompanyCreateEditDto dto) =>
		_mediator.ExecuteCommandAsync(dto.MapCreateCommand(),
									  ModelState,
									  "api/v{0}/Companies/{1}",
									  apiVersion);

	[HttpPut("{id:guid}")]
    [Authorize(Scopes.CompaniesWrite)]
    public Task<IActionResult> Put(Guid id, [FromBody] CompanyCreateEditDto dto) =>
		_mediator.ExecuteCommandAsync(dto.MapEditCommand(id),
									  ModelState,
									  ResponseType.NoContent);

	[HttpDelete("{id:guid}")]
    [Authorize(Scopes.CompaniesWrite)]
    public Task<IActionResult> Delete(Guid id) =>
		_mediator.ExecuteCommandAsync(new CompanyDeleteCommand(id),
									  ModelState);
}