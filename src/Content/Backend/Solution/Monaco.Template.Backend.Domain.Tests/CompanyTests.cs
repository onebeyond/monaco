using FluentAssertions;
using Monaco.Template.Backend.Domain.Model;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Company Entity")]
public class CompanyTests
{
	[Theory(DisplayName = "New company succeeds")]
	[AutoDomainData]
	public void NewCompanySucceeds(string name,
								   string email,
								   string webSiteUrl,
								   Address? address)
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

	[Theory(DisplayName = "New company with empty name fails")]
	[AutoDomainData]
	public void NewCompanyWithEmptyNameFails(string email,
											 string webSiteUrl,
											 Address address)
	{
		var sut = () => new Company(string.Empty,
									email,
									webSiteUrl,
									address);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New company with name too long fails")]
	[AutoDomainData]
	public void NewCompanyWithNameTooLongFails(string email,
											   string webSiteUrl,
											   Address address)
	{
		var sut = () => new Company(new string(It.IsAny<char>(), Company.NameLength + 1),
									email,
									webSiteUrl,
									address);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New company with empty email fails")]
	[AutoDomainData]
	public void NewCompanyWithEmptyEmailFails(string name,
											  string webSiteUrl,
											  Address address)
	{
		var sut = () => new Company(name,
									string.Empty,
									webSiteUrl,
									address);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New company with email too long fails")]
	[AutoDomainData]
	public void NewCompanyWithEmailTooLongFails(string name,
												string webSiteUrl,
												Address address)
	{
		var sut = () => new Company(name,
									new string(It.IsAny<char>(), Company.EmailLength + 1),
									webSiteUrl,
									address);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New company with webSiteUrl too long fails")]
	[AutoDomainData]
	public void NewCompanyWithWebSiteUrlTooLongFails(string name,
													 string email,
													 Address address)
	{
		var sut = () => new Company(name,
									email,
									new string(It.IsAny<char>(), Company.WebSiteUrlLength + 1),
									address);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "Update company succeeds")]
	[AutoDomainData]
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

	[Theory(DisplayName = "Update company with empty name fails")]
	[AutoDomainData]
	public void UpdateCompanyWithEmptyNameFails(Company sut,
												string email,
												string webSiteUrl,
												Address address)
	{
		var call = () => sut.Update(string.Empty,
									email,
									webSiteUrl,
									address);

		call.Should()
			.ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "Update company with name too long fails")]
	[AutoDomainData]
	public void UpdateCompanyWithNameTooLongFails(Company sut,
												  string email,
												  string webSiteUrl,
												  Address address)
	{
		var call = () => sut.Update(new string(It.IsAny<char>(), Company.NameLength + 1),
									email,
									webSiteUrl,
									address);

		call.Should()
			.ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "Update company with empty email fails")]
	[AutoDomainData]
	public void UpdateCompanyWithEmptyEmailFails(Company sut,
												 string name,
												 string webSiteUrl,
												 Address address)
	{
		var call = () => sut.Update(name,
									string.Empty,
									webSiteUrl,
									address);

		call.Should()
			.ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "Update company with email too long fails")]
	[AutoDomainData]
	public void UpdateCompanyWithEmailTooLongFails(Company sut,
												   string name,
												   string webSiteUrl,
												   Address address)
	{
		var call = () => sut.Update(name,
									new string(It.IsAny<char>(), Company.EmailLength + 1),
									webSiteUrl,
									address);

		call.Should()
			.ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "Update company with webSiteUrl too long fails")]
	[AutoDomainData]
	public void UpdateCompanyWithWebSiteUrlTooLongFails(Company sut,
														string name,
														string description,
														Address address)
	{
		var call = () => sut.Update(name,
									description,
									new string(It.IsAny<char>(), Company.WebSiteUrlLength + 1),
									address);

		call.Should()
			.ThrowExactly<ArgumentException>();
	}
#if (filesSupport)

	[Theory(DisplayName = "Add product succeeds")]
	[AutoDomainData]
	public void AddProductSucceeds(Company sut, Product product)
	{
		var originalProductCount = sut.Products.Count;

		sut.AddProduct(product);

		sut.Products
		   .Should()
		   .HaveCount(originalProductCount + 1);
	}

	[Theory(DisplayName = "Remove product succeeds")]
	[AutoDomainData]
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
#endif
}