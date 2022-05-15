using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Image.Queries;

public class GetImageByIdQuery : QueryByIdBase<ImageDto>
{
    public GetImageByIdQuery(Guid id) : base(id)
    {
    }
}