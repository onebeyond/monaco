using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Company.Queries;

public class GetCompanyByIdQuery : QueryByIdBase<CompanyDto?>
{
    public GetCompanyByIdQuery(Guid id) : base(id)
    {
    }
}