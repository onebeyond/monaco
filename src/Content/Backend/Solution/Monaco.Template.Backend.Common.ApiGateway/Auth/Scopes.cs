namespace Monaco.Template.Backend.Common.ApiGateway.Auth;

public static class Scopes
{
	public const string CompaniesRead = "companies:read";
	public const string CompaniesWrite = "companies:write";
#if filesSupport
	public const string FilesRead = "files:read";
	public const string FilesWrite = "files:write";
#endif

	public static List<string> List => new()
									   {
										   CompaniesRead,
										   CompaniesWrite,
#if filesSupport
										   FilesRead,
										   FilesWrite
#endif
									   };
}