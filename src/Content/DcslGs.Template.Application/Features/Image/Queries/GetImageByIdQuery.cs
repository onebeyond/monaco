using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Features.Image.Queries;

public class GetImageByIdQuery : QueryByIdBase<ImageDto>
{
    public GetImageByIdQuery(Guid id) : base(id)
    {
    }
}