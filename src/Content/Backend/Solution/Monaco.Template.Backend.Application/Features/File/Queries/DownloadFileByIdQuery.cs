using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.File.Queries;

public record DownloadFileByIdQuery(Guid Id) : QueryByIdBase<FileDownloadDto?>(Id);