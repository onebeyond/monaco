#if (!disableAuth)
namespace Monaco.Template.Backend.Api.Auth;

public static class Scopes
{
	public const string CompaniesRead = "companies:read";
	public const string CompaniesWrite = "companies:write";
	#if (!excludeFilesSupport)
	public const string FilesWrite = "files:write";
	public const string ProductsWrite = "products:write";
	#endif

	public static List<string> List =>
	[
		CompaniesRead,
		CompaniesWrite,
		#if (!excludeFilesSupport)
		FilesWrite,
		ProductsWrite
		#endif
	];
}
#endif