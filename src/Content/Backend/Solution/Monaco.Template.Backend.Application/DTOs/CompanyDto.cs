namespace Monaco.Template.Backend.Application.DTOs;

public record CompanyDto(Guid Id,
						 string Name,
						 string Email,
						 string WebSiteUrl,
						 string? Street,
						 string? City,
						 string? County,
						 string? PostCode,
						 Guid? CountryId,
						 CountryDto? Country);