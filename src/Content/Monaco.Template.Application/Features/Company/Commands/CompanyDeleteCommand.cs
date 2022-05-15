using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.Company.Commands;

public class CompanyDeleteCommand : CommandBase
{
    public CompanyDeleteCommand(Guid id) : base(id)
    {
    }
}