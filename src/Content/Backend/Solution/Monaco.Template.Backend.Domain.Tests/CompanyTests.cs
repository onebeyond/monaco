using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Company Entity")]
public class CompanyTests
{
	[Theory(DisplayName = "New company succeeds")]
	[AnonymousData]
	public void NewCompanySucceeds(string name,
								   string email,
								   string webSiteUrl,
								   Address address)
	{
		var sut = new Company(name,
							  email,
							  webSiteUrl,
							  address);

		sut.Name.Should().Be(name);
		sut.Email.Should().Be(email);
		sut.WebSiteUrl.Should().Be(webSiteUrl);
		sut.Address.Should().Be(address);
		sut.Version.Should().BeNull();
	}

	[Theory(DisplayName = "Update company succeeds")]
	[AnonymousData]
	public void UpdateCompanySucceeds(Company sut,
									  string name,
									  string email,
									  string webSiteUrl,
									  Address address)
	{
		sut.Update(name,
				   email,
				   webSiteUrl,
				   address);

		sut.Name.Should().Be(name);
		sut.Email.Should().Be(email);
		sut.WebSiteUrl.Should().Be(webSiteUrl);
		sut.Address.Should().Be(address);
		sut.Version.Should().BeNull();
	}

	[Theory(DisplayName = "Add product succeeds")]
	[AnonymousData]
	public void AddProductSucceeds(Company sut, Product product)
	{
		var originalProductCount = sut.Products.Count;

		sut.AddProduct(product);

		sut.Products
		   .Should()
		   .HaveCount(originalProductCount + 1);
	}

	[Theory(DisplayName = "Remove product succeeds")]
	[AnonymousData]
	public void RemoveProductSucceeds(Company sut, Product[] products)
	{
		foreach (var product in products)
			sut.AddProduct(product);
		
		var originalProductCount = sut.Products.Count;

		var deletedProduct = products.First();

		sut.RemoveProduct(deletedProduct);

		sut.Products
		   .Should()
		   .HaveCount(originalProductCount - 1)
		   .And
		   .NotContain(deletedProduct);
	}
}