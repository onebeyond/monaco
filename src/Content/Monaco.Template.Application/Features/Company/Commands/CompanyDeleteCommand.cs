using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.Company.Commands;

public record CompanyDeleteCommand : CommandBase
{
    public CompanyDeleteCommand(Guid id) : base(id)
    {
    }
}