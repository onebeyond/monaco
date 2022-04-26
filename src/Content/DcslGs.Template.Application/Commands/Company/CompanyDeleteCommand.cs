using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Commands.Company;

public record CompanyDeleteCommand : CommandBase
{
    public CompanyDeleteCommand(Guid id) : base(id)
    {
    }
}