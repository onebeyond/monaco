namespace Monaco.Template.Application.DTOs;

public class CompanyDto
{
    public Guid Id {  get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string WebSiteUrl { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string County { get; set; }
    public string PostCode { get; set; }
	public byte[] Version { get; set; }

    public Guid CountryId { get; set; }
    public CountryDto? Country { get; set; }
}