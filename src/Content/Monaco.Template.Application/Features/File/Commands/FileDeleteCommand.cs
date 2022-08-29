using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.File.Commands;

public record FileDeleteCommand : CommandBase
{
    public FileDeleteCommand(Guid id) : base(id)
    {
    }
}