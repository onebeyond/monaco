using Monaco.Template.Backend.Common.Application.Commands;

namespace Monaco.Template.Backend.Application.Features.Company.Commands;

public record CompanyEditCommand(Guid Id,
								 string Name,
								 string Email,
								 string WebSiteUrl,
								 string? Street,
								 string? City,
								 string? County,
								 string? PostCode,
								 Guid? CountryId) : CommandBase(Id);