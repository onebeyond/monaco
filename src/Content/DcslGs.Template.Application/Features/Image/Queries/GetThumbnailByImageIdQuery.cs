using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Features.Image.Queries;

public class GetThumbnailByImageIdQuery : QueryByIdBase<ImageDto>
{
    public GetThumbnailByImageIdQuery(Guid id) : base(id)
    {
    }
}