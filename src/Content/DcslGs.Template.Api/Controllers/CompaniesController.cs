using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DcslGs.Template.Api.Auth;
using DcslGs.Template.Application.Commands.Company;
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Application.DTOs.Extensions;
using DcslGs.Template.Application.Queries.Company;
using DcslGs.Template.Common.Domain.Model;

namespace DcslGs.Template.Api.Controllers;

[ApiVersion("1")]
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
    public async Task<ActionResult<Page<CompanyDto>>> Get()
    {
        var query = new GetCompanyPageQuery(Request.Query);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Scopes.CompaniesRead)]
    public async Task<ActionResult<CompanyDto>> Get(Guid id)
    {
        var query = new GetCompanyByIdQuery(id);
        var item = await _mediator.Send(query);

        if (item == null)
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Scopes.CompaniesWrite)]
    public async Task<ActionResult<Guid>> Post([FromBody] CompanyCreateEditDto dto)
    {
        var cmd = dto.MapCreateCommand();
        var result = await _mediator.Send(cmd);

        if (!result.ValidationResult.IsValid)
        {
            result.ValidationResult.AddToModelState(ModelState, null);
            return BadRequest(ModelState);
        }

        return Created($"api/companies/{result.Result}", result.Result);
    }

    [HttpPut("{id}")]
    [Authorize(Scopes.CompaniesWrite)]
    public async Task<ActionResult> Put(Guid id, [FromBody] CompanyCreateEditDto dto)
    {
        var cmd = dto.MapEditCommand(id);
        var result = await _mediator.Send(cmd);

        if (result.ItemNotFound)
            return NotFound();

        if (!result.ValidationResult.IsValid)
        {
            result.ValidationResult.AddToModelState(ModelState, null);
            return BadRequest(ModelState);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Scopes.CompaniesWrite)]
    public async Task<ActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new CompanyDeleteCommand(id));

        if (result.ItemNotFound)
            return NotFound();

        if (!result.ValidationResult.IsValid)
        {
            result.ValidationResult.AddToModelState(ModelState, null);
            return BadRequest(ModelState);
        }

        return Ok();
    }
}