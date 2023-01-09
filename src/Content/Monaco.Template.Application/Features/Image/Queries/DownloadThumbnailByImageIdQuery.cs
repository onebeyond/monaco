using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.Image.Queries;

public record DownloadThumbnailByImageIdQuery(Guid Id) : QueryByIdBase<FileDownloadDto>(Id);