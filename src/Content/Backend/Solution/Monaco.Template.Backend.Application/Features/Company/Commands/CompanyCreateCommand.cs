using Monaco.Template.Backend.Common.Application.Commands;

namespace Monaco.Template.Backend.Application.Features.Company.Commands;

public record CompanyCreateCommand(string Name,
								   string Email,
								   string WebSiteUrl,
								   string? Street,
								   string? City,
								   string? County,
								   string? PostCode,
								   Guid? CountryId) : CommandBase<Guid>;