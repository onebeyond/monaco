using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Features.File.DTOs;

namespace Monaco.Template.Backend.Application.Features.Product.DTOs;

public record ProductDto(Guid Id,
						 string Title,
						 string Description,
						 decimal Price,
						 Guid CompanyId,
						 CompanyDto? Company,
						 ImageDto[]? Pictures,
						 Guid DefaultPictureId,
						 ImageDto? DefaultPicture);