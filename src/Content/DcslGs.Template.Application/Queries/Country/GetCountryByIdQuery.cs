using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Queries.Country;

public class GetCountryByIdQuery : QueryByIdBase<CountryDto>
{
    public GetCountryByIdQuery(Guid id) : base(id)
    {
    }
}