#if (auth)
namespace Monaco.Template.Backend.Api.Auth;

internal static class Scopes
{
	public const string CompaniesRead = "companies:read";
	public const string CompaniesWrite = "companies:write";
	#if (filesSupport)
	public const string FilesWrite = "files:write";
	public const string ProductsWrite = "products:write";
	#endif

	public static List<string> List =>
	[
		CompaniesRead,
		CompaniesWrite,
		#if (filesSupport)
		FilesWrite,
		ProductsWrite
		#endif
	];
}
#endif