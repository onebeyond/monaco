using AutoFixture.Xunit2;
using AwesomeAssertions;
using Azure.Storage.Blobs;
#if (massTransitIntegration)
using MassTransit.Testing;
#endif
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Application.Features.Product.DTOs;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.IntegrationTests.Apis;
#if (massTransitIntegration || workerService)
using Monaco.Template.Backend.IntegrationTests.Factories;
#endif
#if (massTransitIntegration && (apiService || workerService))
using Monaco.Template.Backend.Messages.V1;
#endif
#if (massTransitIntegration && workerService)
using Monaco.Template.Backend.Worker.Consumers;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Net;
using File = System.IO.File;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Products")]
public class ProductsTests : IntegrationTest
{
	public ProductsTests(AppFixture fixture) : base(fixture)
	{ }

#if (apiService && auth)
	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await RunScriptAsync(@"Scripts\Products.sql");
		var images = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
								  .Set<Image>()
								  .AsNoTracking()
								  .ToListAsync();

		var blobContainerClient = GetBlobContainerClient();
		foreach (var image in images)
		{
			var blobClient = blobContainerClient.GetBlobClient($"{(image.IsTemp ? "temp/" : string.Empty)}{image.Id}");
			await blobClient.UploadAsync(File.OpenRead(@$"Imports\Pictures\{image.Name}{image.Extension}"));
		}
	}
#if (auth)

	private Task SetupAccessToken() =>
		SetupAccessToken([Auth.Auth.Roles.Administrator]);
#endif

	private BlobContainerClient GetBlobContainerClient() =>
		new(Fixture.StorageConnectionString, AppFixture.StorageContainer);

	[Theory(DisplayName = "Get Products page succeeds")]
	[InlineData(false, false, false, null, null, 3)]
	[InlineData(true, true, true, 1, 5, 2)]
	public async Task GetProductsPageSucceeds(bool expandCompany,
											  bool expandPictures,
											  bool expandDefaultPicture,
											  int? offset,
											  int? limit,
											  int expectedItemsCount)
	{
		List<string> expand = [];
		if (expandCompany)
			expand.Add(nameof(ProductDto.Company));
		if (expandPictures)
			expand.Add(nameof(ProductDto.Pictures));
		if (expandDefaultPicture)
			expand.Add(nameof(ProductDto.DefaultPicture));

		var api = GetApi<IProductsApi>(Fixture.WebAppFactory);
		var response = await api.Query(expand.Count > 0 ? [.. expand] : null,
									   offset,
									   limit);

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.OK);

		var result = response.Content;

		result.Should()
			  .NotBeNull();
		result.Items
			  .Should()
			  .HaveCount(expectedItemsCount);
		result.Items
			  .Should()
			  .AllSatisfy(p =>
						  {
							  if (expandCompany)
								  p.Company
								   .Should()
								   .NotBeNull();
							  else
								  p.Company
								   .Should()
								   .BeNull();

							  if (expandPictures)
							  {
								  p.Pictures
								   .Should()
								   .NotBeNull();

								  p.Pictures
								   .Should()
								   .AllSatisfy(pic => pic.Thumbnail
														 .Should()
														 .NotBeNull());
							  }
							  else
								  p.Pictures
								   .Should()
								   .BeNull();

							  if (expandDefaultPicture)
								  p.DefaultPicture
								   .Should()
								   .NotBeNull();
							  else
								  p.DefaultPicture
								   .Should()
								   .BeNull();
						  });
		result.Pager
			  .Should()
			  .BeEquivalentTo(new Pager(offset ?? 0,
										limit ?? 10,
										3));
	}

	[Fact(DisplayName = "Get Product succeeds")]
	public async Task GetProductSucceeds()
	{
		var productId = Guid.Parse("FA934D1C-1E6D-4DD4-ADC2-08DC18C8810C");

		var api = GetApi<IProductsApi>(Fixture.WebAppFactory);
		var response = await api.Get(productId);

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.OK);

		var result = response.Content;
		var product = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
								   .Set<Product>()
								   .Include(x => x.Company)
								   .Include(x => x.DefaultPicture)
								   .Include(x => x.Pictures)
								   .ThenInclude(x => x.Thumbnail)
								   .SingleAsync(c => c.Id == productId);

		result.Should()
			  .NotBeNull();
		result.Title
			  .Should()
			  .Be(product.Title);
		result.Description
			  .Should()
			  .Be(product.Description);
		result.Price
			  .Should()
			  .Be(product.Price);
		result.CompanyId
			  .Should()
			  .Be(product.CompanyId);
		result.Company
			  .Should()
			  .NotBeNull();
		result.DefaultPictureId
			  .Should()
			  .Be(product.DefaultPictureId);
		result.DefaultPictureId
			  .Should<Guid>()
			  .BeOneOf(product.Pictures
							  .Select(p => p.Id));
		result.DefaultPicture
			  .Should()
			  .NotBeNull();
		result.Pictures
			  .Should()
			  .NotBeNullOrEmpty();
		result.Pictures
			  .Should()
			  .HaveCount(2);
		result.Pictures
			  .Should()
			  .AllSatisfy(p =>
						  {
							  p.ThumbnailId
							   .Should()
							   .NotBeNull();

							  p.Thumbnail
							   .Should()
							   .NotBeNull();

							  p.IsTemp
							   .Should()
							   .BeFalse();

							  p.Name
							   .Should()
							   .BeOneOf(product.Pictures
											   .Select(x => x.Name));
						  });
	}

	[Fact(DisplayName = "Download Product's Picture succeeds")]
	public async Task DownloadProductPictureSucceeds()
	{
		var productId = Guid.Parse("FA934D1C-1E6D-4DD4-ADC2-08DC18C8810C");
		var pictureId = Guid.Parse("7D5C57BA-05F4-44FD-832E-5145C5AB0486");

		await DownloadProductPictureTest(productId, pictureId);
	}

	[Fact(DisplayName = "Download Product's Picture Thumbnail succeeds")]
	public async Task DownloadProductPictureThumbnailSucceeds()
	{
		var productId = Guid.Parse("FA934D1C-1E6D-4DD4-ADC2-08DC18C8810C");
		var pictureId = Guid.Parse("7D5C57BA-05F4-44FD-832E-5145C5AB0486");

		await DownloadProductPictureTest(productId, pictureId, true);
	}

	private async Task DownloadProductPictureTest(Guid productId,
												  Guid pictureId,
												  bool? isThumbnail = null)
	{
		var api = GetApi<IProductsApi>(Fixture.WebAppFactory);
		using var response = await api.DownloadPicture(productId, pictureId, isThumbnail);

		var picture = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
								   .Set<Image>()
								   .AsNoTracking()
								   .Where(x => x.Id == pictureId)
								   .Select(x => isThumbnail.HasValue && isThumbnail.Value ? x.Thumbnail! : x)
								   .SingleAsync();

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.OK);
		response.Content
				.Headers
				.ContentType!
				.ToString()
				.Should()
				.Be(picture.ContentType);
		response.Content
				.Headers
				.ContentDisposition!
				.DispositionType
				.Should()
				.Be("attachment");
		response.Content
				.Headers
				.ContentDisposition!
				.FileName
				.Should()
				.Be($"{picture.Name}{picture.Extension}");
		response.Content
				.Headers
				.ContentDisposition!
				.FileNameStar
				.Should()
				.Be($"{picture.Name}{picture.Extension}");
	}

	[Theory(DisplayName = "Create new Product succeeds")]
	[AutoData]
	public async Task CreateNewProductSucceeds(string title,
											   string description,
											   decimal price)
	{
#if (massTransitIntegration)
		var webAppFactory = Fixture.WebAppFactory.GetCustomFactory(b => b.AddMassTransitTestHarnessForWebApp());
#else
		var webAppFactory = Fixture.WebAppFactory;
#endif
#if (workerService && massTransitIntegration)
		var workerServiceFactory = Fixture.WorkerServiceFactory.GetCustomFactory(b => b.AddMassTransitTestHarnessForWorker());
#elif (workerService)
		var workerServiceFactory = Fixture.WorkerServiceFactory;
#endif

		var api = GetApi<IProductsApi>(webAppFactory);
#if (auth)
		await SetupAccessToken();
#endif
#if (apiService && massTransitIntegration)
		var apiTestHarness = webAppFactory.Services.GetTestHarness();
#endif
#if (workerService && massTransitIntegration)
		var serviceTestHarness = workerServiceFactory.Services.GetTestHarness();
#endif

		var dbContext = Fixture.GetDbContext(webAppFactory.Services);
		var tempImages = await dbContext.Set<Image>()
										.Where(i => i.IsTemp && i.ThumbnailId.HasValue)
										.ToListAsync();

		var companyId = Guid.Parse("8CEFE8FA-F747-4A3A-D8C9-08DC18C76CDC");
		var dto = new ProductCreateEditDto(title,
										   description,
										   price,
										   companyId,
										   [.. tempImages.Select(i => i.Id)],
										   tempImages.Last().Id);

		var response = await api.Create(dto);

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.Created);

		var result = response.Content;

		result.Should()
			  .NotBeNull();
		result.Id
			  .Should()
			  .NotBeEmpty();
		response.Headers
				.Location
				.Should()
				.Be(new Uri($"api/v1/Products/{result.Id}", UriKind.Relative));

		var products = await Fixture.GetDbContext(webAppFactory.Services)
									.Set<Product>()
									.Include(x => x.Company)
									.Include(x => x.Pictures)
									.ThenInclude(x => x.Thumbnail)
									.Include(x => x.DefaultPicture)
									.ToListAsync();
		products.Should()
				.HaveCount(4);

		var newProduct = products.SingleOrDefault(c => c.Id == result.Id);
		newProduct.Should()
				  .NotBeNull();
		newProduct.Title
				  .Should()
				  .Be(dto.Title);
		newProduct.Description
				  .Should()
				  .Be(dto.Description);
		newProduct.Price
				  .Should()
				  .Be(dto.Price);
		newProduct.DefaultPicture
				  .Should()
				  .BeEquivalentTo(tempImages.Last());
		newProduct.Pictures
				  .Should()
				  .AllSatisfy(i =>
							  {
								  i.Should()
								   .BeOneOf(tempImages);

								  i.IsTemp
								   .Should()
								   .BeFalse();

								  i.Thumbnail
								   .Should()
								   .NotBeNull();

								  i.ThumbnailId
								   .Should()
								   .NotBeNull();
							  });
#if (massTransitIntegration)
#if (apiService)

		var message = await apiTestHarness.Published
										  .SelectAsync<ProductCreated>()
										  .SingleOrDefaultAsync(x => x.Context.Message.Id == result.Id,
																CancellationToken.None);

		message.Should().NotBeNull();

		var (msgId, msgTitle, msgDescription, msgPrice, msgCompanyId) = message.Context.Message;

		msgId.Should().Be(result.Id);
		msgTitle.Should().Be(dto.Title);
		msgDescription.Should().Be(dto.Description);
		msgCompanyId.Should().Be(dto.CompanyId);
		msgPrice.Should().Be(dto.Price);
#endif
#if (workerService)

		var consumerHarness = serviceTestHarness.GetConsumerHarness<OnProductCreatedThenLongRunningProcess>();
		(await consumerHarness.Consumed.SelectAsync<ProductCreated>().AnyAsync(c => c.Context.Message.Id == result.Id))
			.Should()
			.BeTrue();
#endif
#endif
	}

	[Theory(DisplayName = "Edit existing Product succeeds")]
	[AutoData]
	public async Task EditExistingProductSucceeds(string title,
												  string description,
												  decimal price)
	{
#if (auth)
		await SetupAccessToken();
#endif
		var dbContext = Fixture.GetDbContext(Fixture.WebAppFactory.Services);
		var productId = Guid.Parse("FA934D1C-1E6D-4DD4-ADC2-08DC18C8810C");
		var productPictures = await dbContext.Set<Product>()
											 .AsNoTracking()
											 .Where(x => x.Id == productId)
											 .SelectMany(x => x.Pictures.Select(p => p.Id))
											 .ToListAsync();
		var newPictureId = Guid.Parse("418293F5-3F77-44D5-9B98-4B6A9677D5C7");
		var removedPictureId = productPictures.Last();
		productPictures.Remove(removedPictureId);
		productPictures.Add(newPictureId);
		var companyId = Guid.Parse("95DE146B-86E6-461D-99B3-0CFE0FAA2BAB");
		var dto = new ProductCreateEditDto(title,
										   description,
										   price,
										   companyId,
										   [.. productPictures],
										   newPictureId);

		var api = GetApi<IProductsApi>(Fixture.WebAppFactory);
		var response = await api.Update(productId, dto);

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.NoContent);

		var product = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
								   .Set<Product>()
								   .Include(x => x.Pictures)
								   .Include(x => x.DefaultPicture)
								   .SingleOrDefaultAsync(c => c.Id == productId);
		product.Should()
			   .NotBeNull();
		product!.Title
				.Should()
				.Be(dto.Title);
		product.Description
			   .Should()
			   .Be(dto.Description);
		product.Price
			   .Should()
			   .Be(dto.Price);
		product.CompanyId
			   .Should()
			   .Be(companyId);
		product.DefaultPictureId
			   .Should()
			   .Be(newPictureId);
		product.DefaultPictureId
			   .Should<Guid>()
			   .BeOneOf(productPictures);
		product.DefaultPicture
			   .IsTemp
			   .Should()
			   .BeFalse();
		product.Pictures
			   .Should()
			   .HaveCount(2);
		product.Pictures
			   .Should()
			   .NotContain(x => x.Id == removedPictureId);
		product.Pictures
			   .Should()
			   .Contain(x => x.Id == newPictureId);
		product.Pictures
			   .Should()
			   .AllSatisfy(p =>
						   {
							   p.Id
								.Should<Guid>()
								.BeOneOf(productPictures);

							   p.IsTemp
								.Should()
								.BeFalse();

							   p.Thumbnail
								.Should()
								.NotBeNull();

							   p.ThumbnailId
								.Should()
								.NotBeNull();
						   });
	}

	[Fact(DisplayName = "Delete existing Product succeeds")]
	public async Task DeleteExistingProductSucceeds()
	{
#if (auth)
		await SetupAccessToken();
#endif
		var productId = Guid.Parse("FA934D1C-1E6D-4DD4-ADC2-08DC18C8810C");
		var api = GetApi<IProductsApi>(Fixture.WebAppFactory);
		var response = await api.Delete(productId);

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.OK);

		var products = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
									.Set<Product>()
									.ToListAsync();

		products.Should()
				.HaveCount(2);
		products.Should()
				.NotContain(x => x.Id == productId);
	}

	public override async Task DisposeAsync()
	{
		var container = GetBlobContainerClient();
		await Parallel.ForEachAsync(container.GetBlobs(),
									async (blob, ct) => await container.DeleteBlobAsync(blob.Name, cancellationToken: ct));

		await base.DisposeAsync();
	}
}