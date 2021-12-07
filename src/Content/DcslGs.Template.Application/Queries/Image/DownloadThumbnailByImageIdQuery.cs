using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Queries.Image;

public class DownloadThumbnailByImageIdQuery : QueryByIdBase<FileDownloadDto>
{
    public DownloadThumbnailByImageIdQuery(Guid id) : base(id)
    {
    }
}