namespace Monaco.Template.Backend.Messages.V1;

public record ProductCreated(Guid Id,
							 string Title,
							 string Description,
							 decimal Price,
							 Guid CompanyId);