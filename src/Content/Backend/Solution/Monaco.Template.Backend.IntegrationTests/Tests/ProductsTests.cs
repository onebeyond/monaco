using AutoFixture.Xunit2;
using Azure.Storage.Blobs;
using Dasync.Collections;
using FluentAssertions;
using Flurl.Http;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using System.Net;
#if (workerService && massTransitIntegration)
using Monaco.Template.Backend.Messages.V1;
using Monaco.Template.Backend.Service.Consumers;
#endif
using File = System.IO.File;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Products")]
public class ProductsTests : IntegrationTest
{
	public ProductsTests(AppFixture fixture) : base(fixture)
	{ }

#if (auth)
	protected override bool RequiresAuthentication => true;
#else
	protected override bool RequiresAuthentication => false;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await RunScriptAsync(@"Scripts\Products.sql");
		var images = await GetDbContext().Set<Image>()
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
		new(Fixture.StorageConnectionString,
			AppFixture.StorageContainer);

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
		var response = await CreateRequest(ApiRoutes.Products.Query(expandCompany,
																	expandPictures,
																	expandDefaultPicture,
																	offset,
																	limit)).GetAsync();

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var result = await response.GetJsonAsync<Page<ProductDto>>();

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

		var response = await CreateRequest(ApiRoutes.Products.Get(productId)).GetAsync();

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var result = await response.GetJsonAsync<ProductDto>();
		var product = await GetDbContext().Set<Product>()
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
		var response = await CreateRequest(ApiRoutes.Products.DownloadPicture(productId,
																			  pictureId,
																			  isThumbnail)).GetAsync();
		
		var picture = await GetDbContext().Set<Image>()
										  .AsNoTracking()
										  .Where(x => x.Id == pictureId)
										  .Select(x => isThumbnail.HasValue && isThumbnail.Value ? x.Thumbnail! : x)
										  .SingleAsync();

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);
		response.Headers
				.Should()
				.Contain(("Content-Type", picture.ContentType));
		response.Headers
				.Should()
				.Contain(("Content-Disposition",
						  string.Format("attachment; filename={0}{1}; filename*=UTF-8''{0}{1}",
										picture.Name,
										picture.Extension)));
	}

	[Theory(DisplayName = "Create new Product succeeds")]
	[AutoData]
	public async Task CreateNewProductSucceeds(string title,
											   string description,
											   decimal price)
	{
#if (auth)
		await SetupAccessToken();
#endif
#if (apiService && massTransitIntegration)
		var apiTestHarness = GetApiTestHarness();
#endif
#if (workerService && massTransitIntegration)
		var serviceTestHarness = GetServiceTestHarness();
#endif

		var dbContext = GetDbContext();
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

		var response = await CreateRequest(ApiRoutes.Products.Post()).PostJsonAsync(dto);

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.Created);

		var result = await response.GetStringAsync();

		var id = Guid.Empty;
		result.Should()
			  .Match(value => Guid.TryParse(value.Replace("\"", ""), out id));
		response.Headers
				.Should()
				.Contain(("Location", ApiRoutes.Products.Get(id).ToString()));

		var products = await GetDbContext().Set<Product>()
											.Include(x => x.Company)
											.Include(x => x.Pictures)
											.ThenInclude(x => x.Thumbnail)
											.Include(x => x.DefaultPicture)
											.ToListAsync();
		products.Should()
				 .HaveCount(4);

		var newProduct = products.SingleOrDefault(c => c.Id == id);
		newProduct.Should()
				  .NotBeNull();
		newProduct!.Title
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

		(await apiTestHarness.Published.Any<ProductCreated>())
			.Should()
			.BeTrue();
#endif
#if (workerService)

		(await serviceTestHarness.Consumed.Any<ProductCreated>())
			.Should()
			.BeTrue();

		var consumerHarness = serviceTestHarness.GetConsumerHarness<OnProductCreatedThenLongRunningProcess>();
		(await consumerHarness.Consumed.Any<ProductCreated>())
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
		var dbContext = GetDbContext();
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

		var response = await CreateRequest(ApiRoutes.Products.Put(productId)).PutJsonAsync(dto);

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.NoContent);

		var product = await GetDbContext().Set<Product>()
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
		var response = await CreateRequest(ApiRoutes.Products.Delete(productId)).DeleteAsync();

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var products = await GetDbContext().Set<Product>()
											.ToListAsync();

		products.Should()
				 .HaveCount(2);
		products.Should()
				.NotContain(x => x.Id == productId);
	}

	public override async Task DisposeAsync()
	{
		var container = GetBlobContainerClient();
		await container.GetBlobs()
					   .ParallelForEachAsync(blob => container.DeleteBlobAsync(blob.Name));

		await base.DisposeAsync();
	}
}