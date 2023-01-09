using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.Company.Commands;

public record CompanyEditCommand(Guid Id,
								 string Name,
								 string Email,
								 string WebSiteUrl,
								 string Address,
								 string City,
								 string County,
								 string PostCode,
								 Guid CountryId) : CommandBase(Id);