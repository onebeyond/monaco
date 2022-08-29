using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Image.Queries;

public record GetThumbnailByImageIdQuery : QueryByIdBase<ImageDto>
{
    public GetThumbnailByImageIdQuery(Guid id) : base(id)
    {
    }
}