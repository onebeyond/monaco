namespace Monaco.Template.Backend.Api.DTOs;

public class CompanyCreateEditDto
{
	public string? Name { get; set; }
	public string? Email { get; set; }
	public string? WebSiteUrl { get; set; }
	public string? Street { get; set; }
	public string? City { get; set; }
	public string? County { get; set; }
	public string? PostCode { get; set; }

	public Guid? CountryId { get; set; }
}