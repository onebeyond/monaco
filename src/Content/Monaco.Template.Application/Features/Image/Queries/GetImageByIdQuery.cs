using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.Image.Queries;

public record GetImageByIdQuery(Guid Id) : QueryByIdBase<ImageDto>(Id);