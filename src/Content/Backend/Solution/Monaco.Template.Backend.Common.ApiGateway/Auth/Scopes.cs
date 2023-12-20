namespace Monaco.Template.Backend.Common.ApiGateway.Auth;

public static class Scopes
{
	public const string CompaniesRead = "companies:read";
	public const string CompaniesWrite = "companies:write";
	#if (!excludeFilesSupport)
	public const string FilesRead = "files:read";
	public const string FilesWrite = "files:write";
	#endif

	public static List<string> List =>
	[
		CompaniesRead,
		CompaniesWrite,
		#if (!excludeFilesSupport)
		FilesRead,
		FilesWrite
		#endif
	];
}