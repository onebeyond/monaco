using MediatR;
using Microsoft.AspNetCore.Mvc;
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Application.Queries.Country;

namespace DcslGs.Template.Api.Controllers;

[ApiVersion("1")]
[Route("api/v{apiVersion:apiVersion}/[controller]")]
[ApiController]
public class CountriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CountriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets a list of countries
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<List<CountryDto>>> Get()
    {
        var query = new GetCountryListQuery(Request.Query);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    /// <summary>
    /// Gets a country by Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<CountryDto>> Get(Guid id)
    {
        var query = new GetCountryByIdQuery(id);
        var item = await _mediator.Send(query);

        if (item == null)
            return NotFound();

        return Ok(item);
    }
}