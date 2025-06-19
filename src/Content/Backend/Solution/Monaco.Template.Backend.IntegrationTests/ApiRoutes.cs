using Flurl;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Features.Product.DTOs;

namespace Monaco.Template.Backend.IntegrationTests;

internal static class ApiRoutes
{
	private static readonly Url ApiVersion = "api/v1";
	private const string ExpandParamName = "expand";
	private const string OffsetParamName = "offset";
	private const string LimitParamName = "limit";

	private static Url Expand(this Url url,
							  bool expand,
							  string paramName) =>
		expand
			? url.AppendQueryParam(ExpandParamName, paramName)
			: url;

	private static Url Offset(this Url url, int? offset = null) =>
		url.SetQueryParam(OffsetParamName, offset);

	private static Url Limit(this Url url, int? limit = null) =>
		url.SetQueryParam(LimitParamName, limit);


	public static class Companies
	{
		private static Url Controller => ApiVersion.Clone()
												   .AppendPathSegment(nameof(Companies));

		public static Url Query(bool expandCountry = false, int? offset = null, int? limit = null) =>
			Controller.Expand(expandCountry, nameof(CompanyDto.Country))
					  .Offset(offset)
					  .Limit(limit);
		public static Url Get(Guid id) => Controller.AppendPathSegment(id);
		public static string Post() => Query();
		public static string Put(Guid id) => Get(id);
		public static string Delete(Guid id) => Get(id);
	}

	public static class Countries
	{
		private static Url Controller => ApiVersion.Clone()
												   .AppendPathSegment(nameof(Countries));

		public static Url Query() => Controller;
		public static Url Get(Guid id) => Controller.AppendPathSegment(id);
	}
#if (filesSupport)

	public static class Files
	{
		private static Url Controller => ApiVersion.Clone()
												   .AppendPathSegment(nameof(Files));

		public static Url Post() => Controller;
	}

	public static class Products
	{
		private static Url Controller => ApiVersion.Clone()
												   .AppendPathSegment(nameof(Products));

		public static Url Query(bool expandCompany = false,
								bool expandPictures = false,
								bool expandDefaultPicture = false,
								int? offset = null,
								int? limit = null) =>
			Controller.Expand(expandCompany, nameof(ProductDto.Company))
					  .Expand(expandPictures, nameof(ProductDto.Pictures))
					  .Expand(expandDefaultPicture, nameof(ProductDto.DefaultPicture))
					  .Offset(offset)
					  .Limit(limit);

		public static Url Get(Guid id) => Controller.AppendPathSegment(id);

		public static Url DownloadPicture(Guid productId, Guid pictureId, bool? isThumbnail = null) =>
			Controller.AppendPathSegments(productId, "Pictures", pictureId)
					  .SetQueryParam("thumbnail", isThumbnail);

		public static string Post() => Query();
		public static string Put(Guid id) => Get(id);
		public static string Delete(Guid id) => Get(id);
	}
#endif
}