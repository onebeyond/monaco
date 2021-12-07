using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Queries.Company;

public class GetCompanyByIdQuery : QueryByIdBase<CompanyDto>
{
    public GetCompanyByIdQuery(Guid id) : base(id)
    {
    }
}