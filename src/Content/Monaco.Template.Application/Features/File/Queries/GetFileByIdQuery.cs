using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.File.Queries;

public class GetFileByIdQuery : QueryByIdBase<FileDto>
{
    public GetFileByIdQuery(Guid id) : base(id)
    {
    }
}