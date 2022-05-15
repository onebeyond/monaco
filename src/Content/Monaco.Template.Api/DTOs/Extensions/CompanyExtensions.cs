using Monaco.Template.Application.Features.Company.Commands;

namespace Monaco.Template.Api.DTOs.Extensions;

public static class CompanyExtensions
{
	public static CompanyCreateCommand MapCreateCommand(this CompanyCreateEditDto value) =>
		new(value.Name,
			value.Email,
			value.WebSiteUrl,
			value.Address,
			value.City,
			value.County,
			value.PostCode,
			value.CountryId);

	public static CompanyEditCommand MapEditCommand(this CompanyCreateEditDto value, Guid id) =>
		new(id,
			value.Name,
			value.Email,
			value.WebSiteUrl,
			value.Address,
			value.City,
			value.County,
			value.PostCode,
			value.CountryId);
}