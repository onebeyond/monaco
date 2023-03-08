namespace Monaco.Template.Backend.Application.DTOs;

public class CompanyDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string WebSiteUrl { get; set; } = string.Empty;
	public string? Street { get; set; }
	public string? City { get; set; }
	public string? County { get; set; }
	public string? PostCode { get; set; }

	public Guid? CountryId { get; set; }
	public CountryDto? Country { get; set; }
}