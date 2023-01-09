using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Image.Queries;

public record DownloadThumbnailByImageIdQuery(Guid Id) : QueryByIdBase<FileDownloadDto>(Id);