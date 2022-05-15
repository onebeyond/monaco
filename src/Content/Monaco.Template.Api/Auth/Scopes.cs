namespace Monaco.Template.Api.Auth;

public static class Scopes
{
    public const string CompaniesRead = "companies:read";
    public const string CompaniesWrite = "companies:write";
#if includeFilesSupport
    public const string FilesRead = "files:read";
    public const string FilesWrite = "files:write";
#endif

    public static List<string> List => new()
                                       {
                                           CompaniesRead,
                                           CompaniesWrite,
#if includeFilesSupport
                                           FilesRead,
                                           FilesWrite
#endif
                                       };
}