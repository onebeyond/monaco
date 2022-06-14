namespace Monaco.Template.Application.DependencyInjection;

public class ApplicationOptions
{
	public EntityFrameworkOptions EntityFramework { get; set; } = new();
#if includeFilesSupport
	public BlobStorageOptions BlobStorage { get; set; } = new();
#endif

	public class EntityFrameworkOptions
	{
		public string ConnectionString { get; set; } = string.Empty;
		public bool EnableEfSensitiveLogging { get; set; }	
	}
#if includeFilesSupport

	public class BlobStorageOptions
	{
		public string ConnectionString { get; set; } = string.Empty;
		public string ContainerName { get; set; } = string.Empty;
	}
#endif
}