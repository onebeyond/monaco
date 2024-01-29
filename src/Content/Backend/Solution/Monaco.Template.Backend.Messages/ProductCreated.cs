namespace Monaco.Template.Backend.Messages;

public record ProductCreated(Guid Id,
							 string Title,
							 string Description,
							 decimal Price,
							 Guid CompanyId);