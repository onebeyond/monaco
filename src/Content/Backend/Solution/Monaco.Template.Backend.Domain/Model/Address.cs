using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class Address : ValueObject
{
	protected Address()
	{ }

	public Address(string? street,
				   string? city,
				   string? county,
				   string? postCode,
				   Country country)
	{
		Street = street?.Throw()
					   .IfLongerThan(100);
		City = city?.Throw()
				   .IfLongerThan(100);
		County = county?.Throw()
					   .IfLongerThan(100);
		PostCode = postCode?.Throw()
						   .IfLongerThan(10);
		Country = country.ThrowIfNull();
	}

	public string? Street { get; }
	public string? City { get; }
	public string? County { get; }
	public string? PostCode { get; }

	public Guid CountryId { get; }
	public virtual Country Country { get; }

	protected override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Street;
		yield return City;
		yield return County;
		yield return PostCode;
		yield return Country;
	}
}