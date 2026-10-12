using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Monaco.Eng.RuntimeShapeVerification.Infrastructure;

[ExcludeFromCodeCoverage]
internal static class GeneratedDbAccess
{
	private static string FindMonacoRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
			if (File.Exists(Path.Combine(directory.FullName,
										 ".config",
										 "dotnet-tools.json")))
				return directory.FullName;

		throw new DirectoryNotFoundException("Could not locate monaco tool manifest (.config/dotnet-tools.json).");
	}

	internal static async Task<(string? CreatedBy, DateTimeOffset? CreatedAtUtc)> ReadCompanyStampAsync(
		string connectionString,
		Guid companyId)
	{
		await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = """
							  SELECT TOP (1) CreatedBy, CreatedAtUtc
							  FROM Company
							  WHERE Id = @id
							  """;
		command.Parameters.AddWithValue("@id", companyId);
		await using var reader = await command.ExecuteReaderAsync();
		if (!await reader.ReadAsync())
			return (null, null);

		return (reader.IsDBNull(0) ? null : reader.GetString(0),
				reader.IsDBNull(1) ? null : reader.GetDateTimeOffset(1));
	}

	internal static async Task<int> CountCompaniesByNameAsync(string connectionString, string name)
	{
		await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = """
							  SELECT COUNT(1)
							  FROM Company
							  WHERE Name = @name
							  """;
		command.Parameters.AddWithValue("@name", name);
		var result = await command.ExecuteScalarAsync();
		return Convert.ToInt32(result);
	}

	internal static async Task<(string? CreatedBy, DateTimeOffset? CreatedAtUtc)> ReadProductStampAsync(
		string connectionString,
		Guid productId)
	{
		await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = """
							  SELECT TOP (1) CreatedBy, CreatedAtUtc
							  FROM Product
							  WHERE Id = @id
							  """;
		command.Parameters.AddWithValue("@id", productId);
		await using var reader = await command.ExecuteReaderAsync();
		if (!await reader.ReadAsync())
			return (null, null);

		return (reader.IsDBNull(0) ? null : reader.GetString(0),
				reader.IsDBNull(1) ? null : reader.GetDateTimeOffset(1));
	}

	internal static async Task<(string? CreatedBy, DateTimeOffset? CreatedAtUtc)> ReadFileStampAsync(
		string connectionString,
		Guid fileId)
	{
		await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = """
							  SELECT TOP (1) CreatedBy, CreatedAtUtc
							  FROM [File]
							  WHERE Id = @id
							  """;
		command.Parameters.AddWithValue("@id", fileId);
		await using var reader = await command.ExecuteReaderAsync();
		if (!await reader.ReadAsync())
			return (null, null);

		return (reader.IsDBNull(0) ? null : reader.GetString(0),
				reader.IsDBNull(1) ? null : reader.GetDateTimeOffset(1));
	}

	internal static async Task<int> CountOutboxMessagesAsync(string connectionString)
	{
		await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = """
							  SELECT COUNT(1)
							  FROM OutboxMessage
							  """;
		var result = await command.ExecuteScalarAsync();
		return Convert.ToInt32(result);
	}

	internal static async Task<int> CountInboxDeliveredAsync(string connectionString)
	{
		await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = """
							  SELECT COUNT(1)
							  FROM InboxState
							  WHERE Delivered IS NOT NULL
							  """;
		var result = await command.ExecuteScalarAsync();
		return Convert.ToInt32(result);
	}

	internal static async Task MigrateViaEfAsync(GeneratedShape shape, string connectionString)
	{
		var applicationProject = Path.Combine(shape.ProjectDir("Application"), $"{shape.Name}.Application.csproj");
		var startupSuffix = shape.Options.Api ? "Api" : "Worker";
		var startupProject = Path.Combine(shape.ProjectDir(startupSuffix), $"{shape.Name}.{startupSuffix}.csproj");
		if (!File.Exists(startupProject))
			throw new FileNotFoundException($"EF startup project not found for {shape.Id}.", startupProject);

		var monacoRoot = FindMonacoRoot();
		var startInfo = new ProcessStartInfo("dotnet",
											 $"tool run dotnet-ef -- database update --project \"{applicationProject}\" --startup-project \"{startupProject}\" --connection \"{connectionString}\"")
						{
							WorkingDirectory = monacoRoot,
							RedirectStandardOutput = true,
							RedirectStandardError = true,
							UseShellExecute = false,
							CreateNoWindow = true
						};
		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet ef.");
		var stdout = await process.StandardOutput.ReadToEndAsync();
		var stderr = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();
		if (process.ExitCode != 0)
			throw new InvalidOperationException($"dotnet ef database update failed ({process.ExitCode}):{Environment.NewLine}{stdout}{stderr}");
	}
}