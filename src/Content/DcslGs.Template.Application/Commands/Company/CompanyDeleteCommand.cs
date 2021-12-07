using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Commands.Company;

public class CompanyDeleteCommand : CommandBase
{
    public CompanyDeleteCommand(Guid id) : base(id)
    {
    }
}