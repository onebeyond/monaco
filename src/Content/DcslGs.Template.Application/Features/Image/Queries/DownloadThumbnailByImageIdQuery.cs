using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Features.Image.Queries;

public class DownloadThumbnailByImageIdQuery : QueryByIdBase<FileDownloadDto>
{
    public DownloadThumbnailByImageIdQuery(Guid id) : base(id)
    {
    }
}