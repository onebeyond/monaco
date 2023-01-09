using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.File.Queries;

public record DownloadFileByIdQuery(Guid Id) : QueryByIdBase<FileDownloadDto?>(Id);