namespace Monaco.Template.Backend.Application.DTOs;

public record ProductDto(Guid Id,
						 string Title,
						 string Description,
						 decimal Price,
						 Guid CompanyId,
						 CompanyDto? Company,
						 ImageDto[]? Pictures,
						 Guid DefaultPictureId,
						 ImageDto? DefaultPicture);