using Dawn;
using Monaco.Template.Backend.Common.Domain.Model;

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
		Street = Guard.Argument(street, nameof(street))
					  .MaxLength(100);
		City = Guard.Argument(city, nameof(city))
					.MaxLength(100);
		County = Guard.Argument(county, nameof(county))
					  .MaxLength(100);
		PostCode = Guard.Argument(postCode, nameof(postCode))
						.MaxLength(10);
		Country = Guard.Argument(country, nameof(country))
					   .NotNull();
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