using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Features.Company.Commands;

public class CompanyDeleteCommand : CommandBase
{
    public CompanyDeleteCommand(Guid id) : base(id)
    {
    }
}