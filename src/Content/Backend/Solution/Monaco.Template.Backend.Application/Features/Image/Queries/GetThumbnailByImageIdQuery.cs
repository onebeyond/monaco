using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Image.Queries;

public record GetThumbnailByImageIdQuery(Guid Id) : QueryByIdBase<ImageDto>(Id);