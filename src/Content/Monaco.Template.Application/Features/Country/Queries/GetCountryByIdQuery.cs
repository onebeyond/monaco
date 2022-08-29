using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Country.Queries;

public record GetCountryByIdQuery : QueryByIdBase<CountryDto?>
{
    public GetCountryByIdQuery(Guid id) : base(id)
    {
    }
}