using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.File.Queries;

public record GetFileByIdQuery(Guid Id) : QueryByIdBase<FileDto>(Id);