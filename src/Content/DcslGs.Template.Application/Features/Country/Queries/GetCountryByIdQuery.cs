using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Features.Country.Queries;

public class GetCountryByIdQuery : QueryByIdBase<CountryDto?>
{
    public GetCountryByIdQuery(Guid id) : base(id)
    {
    }
}