using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Features.File.Queries;

public class GetFileByIdQuery : QueryByIdBase<FileDto>
{
    public GetFileByIdQuery(Guid id) : base(id)
    {
    }
}