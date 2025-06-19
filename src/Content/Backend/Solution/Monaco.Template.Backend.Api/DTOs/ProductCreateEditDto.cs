namespace Monaco.Template.Backend.Api.DTOs;

internal record ProductCreateEditDto(string Title,
									 string Description,
									 decimal Price,
									 Guid CompanyId,
									 Guid[] Pictures,
									 Guid DefaultPictureId);