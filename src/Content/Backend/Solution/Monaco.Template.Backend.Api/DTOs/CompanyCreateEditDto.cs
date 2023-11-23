namespace Monaco.Template.Backend.Api.DTOs;

public record CompanyCreateEditDto(string? Name,
								   string? Email,
								   string? WebSiteUrl,
								   string? Street,
								   string? City,
								   string? County,
								   string? PostCode,
								   Guid? CountryId);