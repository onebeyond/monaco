using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Queries.File;

public class DownloadFileByIdQuery : QueryByIdBase<FileDownloadDto?>
{
    public DownloadFileByIdQuery(Guid id) : base(id)
    {
    }
}