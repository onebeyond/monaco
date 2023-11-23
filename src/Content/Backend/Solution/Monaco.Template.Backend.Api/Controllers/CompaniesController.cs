using Asp.Versioning;
using MediatR;
#if (!disableAuth)
using Microsoft.AspNetCore.Authorization;
#endif
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Backend.Api.DTOs;
#if (!disableAuth)
using Monaco.Template.Backend.Api.DTOs.Extensions;
using Monaco.Template.Backend.Api.Auth;
#endif
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Company.Commands;
using Monaco.Template.Backend.Application.Features.Company.Queries;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.Application.Enums;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Api.Controllers;

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
	#if (!disableAuth)
	[Authorize(Scopes.CompaniesRead)]
	#endif
	public Task<ActionResult<Page<CompanyDto>>> Get() =>
		_mediator.ExecuteQueryAsync(new GetCompanyPageQuery(Request.Query));

	[HttpGet("{id:guid}")]
	#if (!disableAuth)
	[Authorize(Scopes.CompaniesRead)]
	#endif
	public Task<ActionResult<CompanyDto?>> Get(Guid id) =>
		_mediator.ExecuteQueryAsync(new GetCompanyByIdQuery(id));

	[HttpPost]
	#if (!disableAuth)
	[Authorize(Scopes.CompaniesWrite)]
	#endif
	public Task<ActionResult<Guid>> Post([FromRoute] ApiVersion apiVersion, [FromBody] CompanyCreateEditDto dto) =>
		_mediator.ExecuteCommandAsync(dto.MapCreateCommand(),
									  ModelState,
									  "api/v{0}/Companies/{1}",
									  apiVersion);

	[HttpPut("{id:guid}")]
	#if (!disableAuth)
	[Authorize(Scopes.CompaniesWrite)]
	#endif
	public Task<IActionResult> Put(Guid id, [FromBody] CompanyCreateEditDto dto) =>
		_mediator.ExecuteCommandAsync(dto.MapEditCommand(id),
									  ModelState,
									  ResponseType.NoContent);

	[HttpDelete("{id:guid}")]
	#if (!disableAuth)
	[Authorize(Scopes.CompaniesWrite)]
	#endif
	public Task<IActionResult> Delete(Guid id) =>
		_mediator.ExecuteCommandAsync(new CompanyDeleteCommand(id),
									  ModelState);
}