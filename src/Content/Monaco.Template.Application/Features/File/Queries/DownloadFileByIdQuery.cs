using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.File.Queries;

public record DownloadFileByIdQuery : QueryByIdBase<FileDownloadDto?>
{
    public DownloadFileByIdQuery(Guid id) : base(id)
    {
    }
}