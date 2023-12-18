namespace Monaco.Template.Backend.Api.DTOs;

public record ProductCreateEditDto(string Title,
								   string Description,
								   decimal Price,
								   Guid CompanyId,
								   Guid[] Pictures,
								   Guid DefaultPictureId);