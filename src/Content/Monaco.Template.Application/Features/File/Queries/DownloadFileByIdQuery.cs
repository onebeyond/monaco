using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.File.Queries;

public class DownloadFileByIdQuery : QueryByIdBase<FileDownloadDto?>
{
    public DownloadFileByIdQuery(Guid id) : base(id)
    {
    }
}