using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Commands.File;

public record FileDeleteCommand : CommandBase
{
    public FileDeleteCommand(Guid id) : base(id)
    {
    }
}