namespace Monaco.Template.Backend.Application.DependencyInjection;

public class ApplicationOptions
{
	public EntityFrameworkOptions EntityFramework { get; set; } = new();
#if (!excludeFilesSupport)
	public BlobStorageOptions BlobStorage { get; set; } = new();
#endif

	public class EntityFrameworkOptions
	{
		public string ConnectionString { get; set; } = string.Empty;
		public bool EnableEfSensitiveLogging { get; set; }
	}
#if (!excludeFilesSupport)

	public class BlobStorageOptions
	{
		public string ConnectionString { get; set; } = string.Empty;
		public string ContainerName { get; set; } = string.Empty;
	}
#endif
}