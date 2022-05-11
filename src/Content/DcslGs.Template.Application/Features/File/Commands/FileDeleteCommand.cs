using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Features.File.Commands;

public class FileDeleteCommand : CommandBase
{
    public FileDeleteCommand(Guid id) : base(id)
    {
    }
}