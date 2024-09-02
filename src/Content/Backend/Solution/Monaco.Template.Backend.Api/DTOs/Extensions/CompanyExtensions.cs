using Monaco.Template.Backend.Application.Features.Company;

namespace Monaco.Template.Backend.Api.DTOs.Extensions;

internal static class CompanyExtensions
{
	public static CreateCompany.Command MapCreateCommand(this CompanyCreateEditDto value) =>
		new(value.Name!,
			value.Email!,
			value.WebSiteUrl!,
			value.Street,
			value.City,
			value.County,
			value.PostCode,
			value.CountryId);

	public static EditCompany.Command MapEditCommand(this CompanyCreateEditDto value, Guid id) =>
		new(id,
			value.Name!,
			value.Email!,
			value.WebSiteUrl!,
			value.Street,
			value.City,
			value.County,
			value.PostCode,
			value.CountryId);
}