using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.Company.Commands;

public record CompanyCreateCommand(string Name,
								   string Email,
								   string WebSiteUrl,
								   string? Street,
								   string? City,
								   string? County,
								   string? PostCode,
                                   Guid? CountryId) : CommandBase<Guid>;