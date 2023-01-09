using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.File.Queries;

public record DownloadFileByIdQuery(Guid Id) : QueryByIdBase<FileDownloadDto?>(Id);