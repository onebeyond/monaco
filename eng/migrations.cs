#:property PublishAot=false
#:property Nullable=enable
#:property ImplicitUsings=enable

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

return MigrationsApp.Execute(args);

static class MigrationsApp
{
	private const string ToolName = "dotnet-ef";
	private const string DesignPackageId = "Microsoft.EntityFrameworkCore.Design";
	private const string RelativeManifest = ".config/dotnet-tools.json";
	private const string RelativeProps = "src/Content/Backend/Solution/Directory.Packages.props";
	private const string RelativeLock = "eng/mssql-server-image.lock.json";
	private const string SourceName = "Monaco.Template.Backend";
	private const string ContextName = "AppDbContext";
	private const int CommandTimeoutMilliseconds = 600_000;
	private const string PinnedVersion = "10.0.2";
	private const string LockedRepository = "mcr.microsoft.com/mssql/server";
	private const string LockedProvenanceTag = "2022-CU26-ubuntu-22.04";
	private const string LockedManifestDigest = "sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89";

	// Closed port so `migrations remove` cannot reach the machine's MonacoTemplate database.
	private const string OfflineConnection =
		"Server=127.0.0.1,1;Initial Catalog=monaco_ef_offline;User ID=sa;Password=unused;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True";

	private static readonly Regex CreateTableName = new(
		"CreateTable\\(\\s*name:\\s*\"(?<name>[^\"]+)\"",
		RegexOptions.Compiled);

	private static readonly Regex ConnectionStringPattern = new(
		"""(?i)(?:Server|Data Source)\s*=\s*[^;\r\n]+(?:;\s*[^;\r\n]*)*""",
		RegexOptions.Compiled);

	private static readonly Fingerprint[] Fingerprints =
	[
		new("Full", true, true, "monaco_fp_full"),
		new("Core", false, false, "monaco_fp_core"),
		new("FilesOnly", true, false, "monaco_fp_files"),
		new("MessagingOnly", false, true, "monaco_fp_messaging")
	];

	private static readonly Dictionary<string, string> DotnetEnvironment = new(StringComparer.Ordinal)
	{
		["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
		["DOTNET_NOLOGO"] = "1",
		["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
		// Persistent MSBuild nodes keep redirected pipes open and hang WaitForExit.
		["MSBUILDDISABLENODEREUSE"] = "1",
		["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
		["DOTNET_ENVIRONMENT"] = "Development",
		["ASPNETCORE_ENVIRONMENT"] = "Development"
	};

	public static int Execute(string[] args) =>
		args switch
		{
			["check"] => RunCheck(),
			["verify"] => RunVerify(),
			["regenerate"] => RunRegenerate(),
			_ => Usage()
		};

	private static int Usage()
	{
		Console.Error.WriteLine("Usage: dotnet run --file eng/migrations.cs -- check|verify|regenerate");
		return 1;
	}

	private static int RunCheck()
	{
		try
		{
			var productRoot = ResolveProductRoot();
			var manifestPath = Path.GetFullPath(Path.Combine(productRoot, RelativeManifest));
			var propsPath = Path.GetFullPath(Path.Combine(productRoot, RelativeProps));

			if (!File.Exists(manifestPath))
			{
				Fail("check", $"Missing local tool manifest at {manifestPath}.");
				return 1;
			}

			if (!File.Exists(propsPath))
			{
				Fail("check", $"Missing Directory.Packages.props at {propsPath}.");
				return 1;
			}

			var expectedVersion = ReadDesignPackageVersion(propsPath);
			if (expectedVersion is null)
			{
				Fail("check", $"Could not read {DesignPackageId} version from {propsPath}.");
				return 1;
			}

			string manifestVersion;
			try
			{
				manifestVersion = ReadSoleDotnetEfVersion(manifestPath);
			}
			catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException)
			{
				Fail("check", $"Local tool manifest pin check failed: {ex.Message}");
				return 1;
			}

			if (!string.Equals(manifestVersion, PinnedVersion, StringComparison.Ordinal))
			{
				Fail("check", $"Manifest pins {ToolName} {manifestVersion}, expected {PinnedVersion}.");
				return 1;
			}

			if (!string.Equals(expectedVersion, PinnedVersion, StringComparison.Ordinal))
			{
				Fail("check", $"{DesignPackageId} is {expectedVersion}, expected {PinnedVersion}.");
				return 1;
			}

			if (!TryRestoreLocalTools(productRoot, manifestPath, out var restoreError))
			{
				Fail("check", restoreError);
				return 1;
			}

			if (!TryReadEffectiveToolVersion(manifestPath, out var effectiveVersion, out var probeError))
			{
				Fail("check", probeError);
				return 1;
			}

			if (!string.Equals(effectiveVersion, expectedVersion, StringComparison.Ordinal))
			{
				Fail("check", $"Effective {ToolName} version token is '{effectiveVersion}', expected '{expectedVersion}'.");
				return 1;
			}

			Console.WriteLine($"OK: repository-local {ToolName} {expectedVersion} matches {DesignPackageId}.");
			return 0;
		}
		catch (Exception ex)
		{
			Fail("check", ex.Message);
			return 1;
		}
	}

	private static int RunVerify()
	{
		var check = RunCheck();
		if (check != 0)
			return check;

		string? secret = null;
		string? container = null;
		string? tempRoot = null;
		var exit = 0;
		string? cleanup = null;
		try
		{
			var productRoot = ResolveProductRoot();
			var image = ReadLockedImage(productRoot);
			var shippedIds = Fingerprints.ToDictionary(fingerprint => fingerprint.Name, fingerprint => ReadShippedId(fingerprint.Destination(productRoot)), StringComparer.Ordinal);
			tempRoot = CreateTempRoot(productRoot);
			container = "monaco-mig-" + Guid.NewGuid().ToString("N")[..12];
			var port = FreeTcpPort();
			secret = CreateSaPassword();
			Step("verify", "pulling SQL Server image");
			Run("docker", ["pull", image], productRoot, null, "docker pull", secret);
			Step("verify", "starting SQL Server container");
			StartContainer(container, port, secret, image, productRoot);

			var hive = InstallHive(productRoot, tempRoot);
			foreach (var fingerprint in Fingerprints)
			{
				var name = "VerifyApi" + fingerprint.Name;
				var output = Path.Combine(tempRoot, "api-" + fingerprint.Name.ToLowerInvariant());
				Step("verify", $"generating API {fingerprint.Name}");
				Generate(hive, productRoot, name, output, fingerprint, api: true, worker: false);
				var application = ProjectFile(output, name, "Application");
				var startup = ProjectFile(output, name, "Api");
				if (!File.Exists(startup))
					throw new ToolFailureException($"API startup project is missing for {fingerprint.Name}.");

				RestoreProject(output, startup, $"API {fingerprint.Name}");
				var migrationsDir = MigrationsDirectory(output, name);
				var implementation = AssertTrio(migrationsDir, shippedIds[fingerprint.Name], $"API {fingerprint.Name}");
				AssertModel(implementation, fingerprint, $"API {fingerprint.Name}");
				AssertNoPending(productRoot, application, startup, $"API {fingerprint.Name}", secret);
				AssertScriptNamesId(productRoot, application, startup, shippedIds[fingerprint.Name], $"API {fingerprint.Name}", secret);
			}

			foreach (var fingerprint in Fingerprints.Where(fingerprint => fingerprint.Name is "Core" or "Full"))
			{
				var name = "VerifyWorker" + fingerprint.Name;
				var output = Path.Combine(tempRoot, "worker-" + fingerprint.Name.ToLowerInvariant());
				Step("verify", $"generating Worker {fingerprint.Name}");
				Generate(hive, productRoot, name, output, fingerprint, api: false, worker: true);
				var api = ProjectFile(output, name, "Api");
				var startup = ProjectFile(output, name, "Worker");
				if (File.Exists(api))
					throw new ToolFailureException($"Worker {fingerprint.Name} generated an API project.");
				if (!File.Exists(startup))
					throw new ToolFailureException($"Worker startup project is missing for {fingerprint.Name}.");

				RestoreProject(output, startup, $"Worker {fingerprint.Name}");
				var implementation = AssertTrio(MigrationsDirectory(output, name), shippedIds[fingerprint.Name], $"Worker {fingerprint.Name}");
				AssertModel(implementation, fingerprint, $"Worker {fingerprint.Name}");
				AssertNoPending(productRoot, ProjectFile(output, name, "Application"), startup, $"Worker {fingerprint.Name}", secret);
			}

			Step("verify", "waiting for SQL Server");
			WaitUntilReady(container, productRoot, secret);
			foreach (var fingerprint in Fingerprints)
			{
				Step("verify", $"applying API {fingerprint.Name}");
				Query(container, "master", $"SET NOCOUNT ON; IF DB_ID(N'{fingerprint.Database}') IS NULL CREATE DATABASE [{fingerprint.Database}];", productRoot, secret);
				var name = "VerifyApi" + fingerprint.Name;
				var output = Path.Combine(tempRoot, "api-" + fingerprint.Name.ToLowerInvariant());
				var connection = ConnectionString(port, fingerprint.Database, secret);
				ApplyDatabase(productRoot, ProjectFile(output, name, "Application"), ProjectFile(output, name, "Api"), connection, fingerprint.Name, secret);
				InspectDatabase(container, fingerprint, shippedIds[fingerprint.Name], productRoot, secret);
			}

			Console.WriteLine("OK: verified 4 API fingerprints and 2 worker-only probes.");
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"migrations verify failed: {Scrub(ex.Message, secret)}");
			exit = 1;
		}
		finally
		{
			cleanup = Cleanup(container, tempRoot, productRoot: SafeProductRoot(), secret);
		}

		if (cleanup is not null)
		{
			Console.Error.WriteLine($"migrations verify cleanup failed: {Scrub(cleanup, secret)}");
			return 1;
		}

		return exit;
	}

	private static int RunRegenerate()
	{
		var check = RunCheck();
		if (check != 0)
			return check;

		string? tempRoot = null;
		var exit = 0;
		string? cleanup = null;
		try
		{
			var productRoot = ResolveProductRoot();
			tempRoot = CreateTempRoot(productRoot);
			var hive = InstallHive(productRoot, tempRoot);
			var generated = new List<(Fingerprint Fingerprint, string MigrationsDir, string Id)>();
			var ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (var fingerprint in Fingerprints)
			{
				if (ids.Count > 0)
					Thread.Sleep(1100);

				var output = Path.Combine(tempRoot, "regen-" + fingerprint.Name.ToLowerInvariant());
				Step("regenerate", $"generating {fingerprint.Name}");
				Generate(hive, productRoot, SourceName, output, fingerprint, api: true, worker: false);
				var application = ProjectFile(output, SourceName, "Application");
				var startup = ProjectFile(output, SourceName, "Api");
				RestoreProject(output, startup, fingerprint.Name);
				Step("regenerate", $"replacing Init for {fingerprint.Name}");
				RemoveMigration(productRoot, application, startup);
				AddMigration(productRoot, application, startup);
				var migrationsDir = MigrationsDirectory(output, SourceName);
				var id = ReadSingleInitId(migrationsDir, fingerprint.Name);
				var implementation = AssertTrio(migrationsDir, id, fingerprint.Name);
				AssertModel(implementation, fingerprint, fingerprint.Name);
				AssertNoPending(productRoot, application, startup, fingerprint.Name, secret: null);
				if (!ids.Add(id))
					throw new ToolFailureException($"Fingerprint {fingerprint.Name} reused migration id {id}.");

				generated.Add((fingerprint, migrationsDir, id));
			}

			var destinations = generated.Select(item => item.Fingerprint.Destination(productRoot)).ToArray();
			var snapshot = Capture(destinations);
			try
			{
				foreach (var item in generated)
					ReplaceTrio(item.Fingerprint.Destination(productRoot), item.MigrationsDir);
			}
			catch (Exception ex)
			{
				try
				{
					Restore(snapshot);
				}
				catch (Exception restoreEx)
				{
					throw new ToolFailureException(
						$"Replacement failed ({ex.Message}) and restoring the snapshot failed ({restoreEx.Message}).");
				}

				throw new ToolFailureException($"Replacement failed and the snapshot was restored: {ex.Message}");
			}

			Console.WriteLine("OK: regenerated 4 migration trios with ids " + string.Join(", ", ids.OrderBy(id => id, StringComparer.Ordinal)) + ".");
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"migrations regenerate failed: {Scrub(ex.Message, secret: null)}");
			exit = 1;
		}
		finally
		{
			cleanup = Cleanup(container: null, tempRoot, productRoot: SafeProductRoot(), secret: null);
		}

		if (cleanup is not null)
		{
			Console.Error.WriteLine($"migrations regenerate cleanup failed: {cleanup}");
			return 1;
		}

		return exit;
	}

	private static void RestoreProject(string output, string project, string label) =>
		Run("dotnet",
			["restore", project, "--nologo"],
			output,
			DotnetEnvironment,
			$"dotnet restore {label}",
			secret: null);

	private static void RemoveMigration(string productRoot, string application, string startup)
	{
		var environment = new Dictionary<string, string>(DotnetEnvironment, StringComparer.Ordinal)
		{
			["ConnectionStrings__AppDbContext"] = OfflineConnection
		};
		// Without --force, EF aborts when the startup database is unreachable. Force still only reverts a database
		// when the history row is present; the offline connection keeps that check off MonacoTemplate.
		Run("dotnet",
			[
				"tool", "run", ToolName, "--",
				"migrations", "remove",
				"--project", application,
				"--startup-project", startup,
				"--context", ContextName,
				"--force"
			],
			productRoot,
			environment,
			"migrations remove",
			secret: null);
	}

	private static void AddMigration(string productRoot, string application, string startup) =>
		Run("dotnet",
			[
				"tool", "run", ToolName, "--",
				"migrations", "add", "Init",
				"--project", application,
				"--startup-project", startup,
				"--context", ContextName,
				"--output-dir", "Persistence/Migrations"
			],
			productRoot,
			DotnetEnvironment,
			"migrations add Init",
			secret: null);

	private static void AssertNoPending(string productRoot, string application, string startup, string label, string? secret)
	{
		var result = Run("dotnet",
						 [
							 "tool", "run", ToolName, "--",
							 "migrations", "has-pending-model-changes",
							 "--project", application,
							 "--startup-project", startup,
							 "--context", ContextName
						 ],
						 productRoot,
						 DotnetEnvironment,
						 $"{label} has-pending-model-changes",
						 secret,
						 allowNonZero: true);
		if (result.ExitCode != 0)
			throw new ToolFailureException($"{label} has-pending-model-changes exited {result.ExitCode}.{Environment.NewLine}{result.Stdout}{result.Stderr}");
	}

	private static void AssertScriptNamesId(string productRoot,
											string application,
											string startup,
											string migrationId,
											string label,
											string? secret)
	{
		var result = Run("dotnet",
						 [
							 "tool", "run", ToolName, "--",
							 "migrations", "script",
							 "--idempotent",
							 "--project", application,
							 "--startup-project", startup,
							 "--context", ContextName
						 ],
						 productRoot,
						 DotnetEnvironment,
						 $"{label} migrations script",
						 secret);
		if (!result.Stdout.Contains(migrationId + "_Init", StringComparison.Ordinal))
			throw new ToolFailureException($"{label} idempotent script does not name {migrationId}_Init.");
	}

	private static void ApplyDatabase(string productRoot,
									  string application,
									  string startup,
									  string connection,
									  string label,
									  string secret) =>
		Run("dotnet",
			[
				"tool", "run", ToolName, "--",
				"database", "update",
				"--project", application,
				"--startup-project", startup,
				"--context", ContextName,
				"--connection", connection
			],
			productRoot,
			DotnetEnvironment,
			$"{label} database update",
			secret);

	private static string InstallHive(string productRoot, string tempRoot)
	{
		var hive = Path.Combine(tempRoot, "hive");
		Directory.CreateDirectory(hive);
		var template = Path.Combine(productRoot, "src", "Content", "Backend", "Solution");
		Run("dotnet",
			["new", "install", template, "--debug:custom-hive", hive],
			tempRoot,
			DotnetEnvironment,
			"dotnet new install",
			secret: null);
		return hive;
	}

	private static void Generate(string hive,
								 string productRoot,
								 string name,
								 string output,
								 Fingerprint fingerprint,
								 bool api,
								 bool worker) =>
		Run("dotnet",
			[
				"new", "monaco-backend-solution",
				"--name", name,
				"--output", output,
				"--debug:custom-hive", hive,
				"--auth", "false",
				"--keycloakConfig", "false",
				"--tests", "false",
				"--apiGateway", "false",
				"--apiService", api ? "true" : "false",
				"--workerService", worker ? "true" : "false",
				"--filesSupport", fingerprint.Files ? "true" : "false",
				"--massTransitIntegration", fingerprint.Messaging ? "true" : "false"
			],
			productRoot,
			DotnetEnvironment,
			$"dotnet new {name}",
			secret: null);

	private static void StartContainer(string name, int port, string password, string image, string workingDirectory)
	{
		var startInfo = new ProcessStartInfo("docker")
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		startInfo.Environment["MSSQL_SA_PASSWORD"] = password;
		foreach (var argument in new[]
				 {
					 "run", "--name", name, "-d",
					 "-p", $"127.0.0.1:{port}:1433",
					 "-e", "ACCEPT_EULA=Y",
					 "-e", "MSSQL_SA_PASSWORD",
					 image
				 })
			startInfo.ArgumentList.Add(argument);

		var result = Wait(startInfo, "docker run", password);
		if (result.ExitCode != 0)
			throw new ToolFailureException($"docker run failed (exit {result.ExitCode}).{Environment.NewLine}{result.Stdout}{result.Stderr}");
	}

	private static void WaitUntilReady(string container, string workingDirectory, string secret)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(CommandTimeoutMilliseconds);
		string? last = null;
		while (DateTime.UtcNow < deadline)
		{
			CommandResult status;
			try
			{
				status = Run("docker",
							 ["inspect", "-f", "{{.State.Status}}", container],
							 workingDirectory,
							 null,
							 "docker inspect",
							 secret,
							 allowNonZero: true,
							 timeoutMilliseconds: 30_000);
			}
			catch (ToolFailureException ex) when (IsTimeout(ex))
			{
				last = ex.Message;
				Thread.Sleep(2000);
				continue;
			}

			var state = status.Stdout.Trim();
			if (status.ExitCode == 0 && state is "exited" or "dead")
				throw new ToolFailureException($"SQL Server container {container} exited before it became ready.");

			CommandResult probe;
			try
			{
				probe = Run("docker",
							SqlcmdArgs(container, "master", "SET NOCOUNT ON; SELECT 1;"),
							workingDirectory,
							null,
							"sqlcmd readiness",
							secret,
							allowNonZero: true,
							timeoutMilliseconds: 20_000);
			}
			catch (ToolFailureException ex) when (IsTimeout(ex))
			{
				last = ex.Message;
				Thread.Sleep(2000);
				continue;
			}
			if (probe.ExitCode == 0 && probe.Stdout.Contains('1', StringComparison.Ordinal))
				return;

			last = probe.Stdout + probe.Stderr;
			if (last.Contains("No such file", StringComparison.OrdinalIgnoreCase) ||
				last.Contains("executable file not found", StringComparison.OrdinalIgnoreCase))
				throw new ToolFailureException($"sqlcmd is not available in the SQL Server image.{Environment.NewLine}{last}");

			Thread.Sleep(2000);
		}

		throw new ToolFailureException($"SQL Server container {container} did not become ready.{Environment.NewLine}{last}");
	}

	private static void InspectDatabase(string container, Fingerprint fingerprint, string migrationId, string workingDirectory, string secret)
	{
		var tables = Query(container,
						   fingerprint.Database,
						   "SET NOCOUNT ON; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;",
						   workingDirectory,
						   secret)
			.Select(row => row[0])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var expected = fingerprint.Tables().Append("__EFMigrationsHistory").ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (!tables.SetEquals(expected))
			throw new ToolFailureException($"Fingerprint {fingerprint.Name} tables were {FormatSet(tables)}, expected {FormatSet(expected)}.");

		var history = Query(container,
							fingerprint.Database,
							"SET NOCOUNT ON; SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;",
							workingDirectory,
							secret)
			.Select(row => row[0])
			.ToArray();
		if (history.Length != 1 || !string.Equals(history[0], migrationId + "_Init", StringComparison.Ordinal))
			throw new ToolFailureException($"Fingerprint {fingerprint.Name} history was [{string.Join(", ", history)}], expected {migrationId}_Init.");

		var columns = Query(container,
							fingerprint.Database,
							"""
							SET NOCOUNT ON;
							SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE,
							       COALESCE(CONVERT(varchar(20), CHARACTER_MAXIMUM_LENGTH), ''),
							       COALESCE(CONVERT(varchar(20), DATETIME_PRECISION), ''),
							       IS_NULLABLE
							FROM INFORMATION_SCHEMA.COLUMNS
							WHERE TABLE_SCHEMA = 'dbo'
							  AND COLUMN_NAME IN ('CreatedAtUtc', 'CreatedBy', 'ModifiedAtUtc', 'ModifiedBy')
							ORDER BY TABLE_NAME, COLUMN_NAME;
							""",
							workingDirectory,
							secret);
		AssertSqlStamps(fingerprint, columns);
	}

	private static void AssertSqlStamps(Fingerprint fingerprint, IReadOnlyList<string[]> rows)
	{
		var stamped = StampedTables(fingerprint);
		var byTable = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
		foreach (var row in rows)
		{
			if (row.Length != 6)
				throw new ToolFailureException($"Fingerprint {fingerprint.Name} returned an unreadable stamp row [{string.Join(" | ", row)}].");

			if (!byTable.TryGetValue(row[0], out var columns))
			{
				columns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
				byTable[row[0]] = columns;
			}

			columns[row[1]] = row;
		}

		foreach (var table in stamped)
		{
			if (!byTable.TryGetValue(table, out var columns))
				throw new ToolFailureException($"Fingerprint {fingerprint.Name} table {table} has no stamp columns.");

			AssertSqlStamp(fingerprint.Name, table, columns, "CreatedAtUtc", "datetimeoffset", length: "", precision: "7", nullable: false);
			AssertSqlStamp(fingerprint.Name, table, columns, "CreatedBy", "nvarchar", length: "512", precision: "", nullable: true);
			AssertSqlStamp(fingerprint.Name, table, columns, "ModifiedAtUtc", "datetimeoffset", length: "", precision: "7", nullable: true);
			AssertSqlStamp(fingerprint.Name, table, columns, "ModifiedBy", "nvarchar", length: "512", precision: "", nullable: true);
			if (columns.Count != 4)
				throw new ToolFailureException($"Fingerprint {fingerprint.Name} table {table} has unexpected stamp columns.");
		}

		foreach (var table in byTable.Keys)
		{
			if (!stamped.Contains(table))
				throw new ToolFailureException($"Fingerprint {fingerprint.Name} table {table} must not define stamp columns.");
		}
	}

	private static void AssertSqlStamp(string fingerprint,
									   string table,
									   Dictionary<string, string[]> columns,
									   string column,
									   string dataType,
									   string length,
									   string precision,
									   bool nullable)
	{
		if (!columns.TryGetValue(column, out var row))
			throw new ToolFailureException($"Fingerprint {fingerprint} table {table} is missing {column}.");

		if (!row[2].Equals(dataType, StringComparison.OrdinalIgnoreCase) ||
			!row[3].Equals(length, StringComparison.Ordinal) ||
			!row[4].Equals(precision, StringComparison.Ordinal) ||
			!row[5].Equals(nullable ? "YES" : "NO", StringComparison.OrdinalIgnoreCase))
			throw new ToolFailureException(
				$"Fingerprint {fingerprint} table {table} column {column} was type {row[2]}, length {row[3]}, precision {row[4]}, nullable {row[5]}.");
	}

	private static IReadOnlyList<string[]> Query(string container, string database, string sql, string workingDirectory, string secret)
	{
		var result = Run("docker", SqlcmdArgs(container, database, sql), workingDirectory, null, "sqlcmd", secret);
		var rows = new List<string[]>();
		foreach (var raw in result.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
		{
			var line = raw.Trim();
			if (line.Length == 0 || line.StartsWith('(') || line.All(character => character is '-' or '|' or ' '))
				continue;
			if (!line.Contains('|', StringComparison.Ordinal) && line.Contains(' ', StringComparison.Ordinal))
				continue;

			rows.Add(line.Split('|', StringSplitOptions.TrimEntries));
		}

		return rows;
	}

	private static string[] SqlcmdArgs(string container, string database, string sql)
	{
		var command =
			$"SQLCMDPASSWORD=\"$MSSQL_SA_PASSWORD\" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -d {database} -C -b -h-1 -W -s \"|\" -Q {QuoteBash(sql)}";
		return ["exec", container, "bash", "-lc", command];
	}

	private static string QuoteBash(string sql) =>
		"'" + sql.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

	private static string AssertTrio(string migrationsDir, string expectedId, string label)
	{
		if (!Directory.Exists(migrationsDir))
			throw new ToolFailureException($"{label} migrations directory is missing.");

		var names = Directory.GetFiles(migrationsDir)
							 .Select(Path.GetFileName)
							 .OrderBy(name => name, StringComparer.Ordinal)
							 .ToArray();
		string[] expected =
		[
			$"{expectedId}_Init.cs",
			$"{expectedId}_Init.Designer.cs",
			"AppDbContextModelSnapshot.cs"
		];
		Array.Sort(expected, StringComparer.Ordinal);
		if (!names.SequenceEqual(expected!, StringComparer.Ordinal))
			throw new ToolFailureException($"{label} migrations were [{string.Join(", ", names)}], expected the {expectedId} Init trio.");

		var designer = File.ReadAllText(Path.Combine(migrationsDir, $"{expectedId}_Init.Designer.cs"));
		if (!designer.Contains($"[Migration(\"{expectedId}_Init\")]", StringComparison.Ordinal))
			throw new ToolFailureException($"{label} designer does not name {expectedId}_Init.");

		return File.ReadAllText(Path.Combine(migrationsDir, $"{expectedId}_Init.cs"));
	}

	private static void AssertModel(string implementation, Fingerprint fingerprint, string label)
	{
		var tables = CreateTableName.Matches(implementation)
									.Select(match => match.Groups["name"].Value)
									.Distinct(StringComparer.Ordinal)
									.ToHashSet(StringComparer.Ordinal);
		var expected = fingerprint.Tables().ToHashSet(StringComparer.Ordinal);
		if (!tables.SetEquals(expected))
			throw new ToolFailureException($"{label} migration tables were {FormatSet(tables)}, expected {FormatSet(expected)}.");

		foreach (var table in StampedTables(fingerprint))
			AssertSourceStamp(implementation, table, label, present: true);

		foreach (var table in new[] { "Country", "ProductPicture", "InboxState", "OutboxState", "OutboxMessage" })
		{
			if (!expected.Contains(table))
				continue;

			AssertSourceStamp(implementation, table, label, present: false);
		}
	}

	private static void AssertSourceStamp(string implementation, string table, string label, bool present)
	{
		var block = TableBlock(implementation, table);
		if (block is null)
			throw new ToolFailureException($"{label} migration is missing table {table}.");

		foreach (var column in new[] { "CreatedAtUtc", "CreatedBy", "ModifiedAtUtc", "ModifiedBy" })
		{
			var statement = ColumnStatement(block, column);
			if (!present)
			{
				if (statement is not null || block.Contains(column, StringComparison.Ordinal))
					throw new ToolFailureException($"{label} table {table} must not define {column}.");
				continue;
			}

			if (statement is null)
				throw new ToolFailureException($"{label} table {table} is missing {column}.");

			var matches = column switch
			{
				"CreatedAtUtc" => statement.Contains("datetimeoffset(7)", StringComparison.Ordinal) &&
								  statement.Contains("nullable: false", StringComparison.Ordinal),
				"ModifiedAtUtc" => statement.Contains("datetimeoffset(7)", StringComparison.Ordinal) &&
								   statement.Contains("nullable: true", StringComparison.Ordinal),
				"CreatedBy" or "ModifiedBy" => statement.Contains("nvarchar(512)", StringComparison.Ordinal) &&
											   ContainsExactToken(statement, "maxLength: 512") &&
											   statement.Contains("nullable: true", StringComparison.Ordinal),
				_ => false
			};
			if (!matches)
				throw new ToolFailureException($"{label} table {table} column {column} does not match the stamp contract.");
		}
	}

	private static string? TableBlock(string implementation, string table)
	{
		var marker = $"name: \"{table}\"";
		var start = implementation.IndexOf("CreateTable(", StringComparison.Ordinal);
		while (start >= 0)
		{
			var next = implementation.IndexOf("CreateTable(", start + 1, StringComparison.Ordinal);
			var end = next >= 0 ? next : implementation.Length;
			var block = implementation[start..end];
			if (block.Contains(marker, StringComparison.Ordinal))
				return block;
			start = next;
		}

		return null;
	}

	private static bool ContainsExactToken(string text, string token)
	{
		var index = text.IndexOf(token, StringComparison.Ordinal);
		if (index < 0)
			return false;

		var after = index + token.Length;
		return after >= text.Length || !char.IsDigit(text[after]);
	}

	private static bool IsTimeout(ToolFailureException exception) =>
		exception.Message.Contains("did not finish within", StringComparison.Ordinal);

	private static string? ColumnStatement(string block, string column)
	{
		var marker = column + " = table.Column<";
		var start = block.IndexOf(marker, StringComparison.Ordinal);
		if (start < 0)
			return null;

		// The last column in a CreateTable call has no trailing comma, so read the statement line.
		var lineEnd = block.IndexOf('\n', start);
		return lineEnd < 0 ? block[start..] : block[start..lineEnd];
	}

	private static HashSet<string> StampedTables(Fingerprint fingerprint)
	{
		var stamped = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Company" };
		if (fingerprint.Files)
		{
			stamped.Add("File");
			stamped.Add("Product");
		}

		return stamped;
	}

	private static string ReadShippedId(string destination) =>
		ReadSingleInitId(destination, destination);

	private static string ReadSingleInitId(string directory, string label)
	{
		if (!Directory.Exists(directory))
			throw new ToolFailureException($"Migration directory is missing for {label}.");

		var inits = Directory.GetFiles(directory, "*_Init.cs")
							 .Where(path => path.EndsWith("_Init.cs", StringComparison.Ordinal) &&
											!path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
							 .ToArray();
		if (inits.Length != 1)
			throw new ToolFailureException($"Expected one Init implementation for {label}.");

		var name = Path.GetFileName(inits[0])!;
		return name[..^"_Init.cs".Length];
	}

	private static TrioSnapshot Capture(IReadOnlyList<string> directories)
	{
		var files = new List<SavedFile>();
		foreach (var directory in directories)
		{
			if (!Directory.Exists(directory))
				throw new ToolFailureException($"Migration destination is missing: {directory}");

			foreach (var file in Directory.GetFiles(directory))
				files.Add(new SavedFile(file, File.ReadAllBytes(file)));
		}

		return new TrioSnapshot(directories, files);
	}

	private static void Restore(TrioSnapshot snapshot)
	{
		foreach (var directory in snapshot.Directories)
		{
			Directory.CreateDirectory(directory);
			foreach (var file in Directory.GetFiles(directory))
				File.Delete(file);
		}

		foreach (var file in snapshot.Files)
		{
			var parent = Path.GetDirectoryName(file.Path);
			if (!string.IsNullOrEmpty(parent))
				Directory.CreateDirectory(parent);
			File.WriteAllBytes(file.Path, file.Bytes);
		}
	}

	private static void ReplaceTrio(string destination, string source)
	{
		Directory.CreateDirectory(destination);
		foreach (var file in Directory.GetFiles(destination))
			File.Delete(file);
		foreach (var file in Directory.GetFiles(source))
			File.Copy(file, Path.Combine(destination, Path.GetFileName(file)!), overwrite: false);
	}

	private static string ReadLockedImage(string productRoot)
	{
		var path = Path.Combine(productRoot, RelativeLock);
		if (!File.Exists(path))
			throw new ToolFailureException($"SQL Server image lock is missing at {path}.");

		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(File.ReadAllText(path));
		}
		catch (JsonException ex)
		{
			throw new ToolFailureException($"SQL Server image lock is not valid JSON: {ex.Message}");
		}

		using (document)
		{
			if (document.RootElement.ValueKind != JsonValueKind.Object)
				throw new ToolFailureException("SQL Server image lock must be a JSON object.");

			var expected = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["repository"] = LockedRepository,
				["provenanceTag"] = LockedProvenanceTag,
				["manifestDigest"] = LockedManifestDigest
			};
			var actual = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var property in document.RootElement.EnumerateObject())
			{
				if (!actual.TryAdd(property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? "" : "\0"))
					throw new ToolFailureException($"SQL Server image lock repeats '{property.Name}'.");
				if (property.Value.ValueKind != JsonValueKind.String)
					throw new ToolFailureException($"SQL Server image lock property '{property.Name}' must be a string.");
			}

			foreach (var pair in expected)
			{
				if (!actual.TryGetValue(pair.Key, out var value))
					throw new ToolFailureException($"SQL Server image lock is missing '{pair.Key}'.");
				if (!string.Equals(value, pair.Value, StringComparison.Ordinal))
					throw new ToolFailureException($"SQL Server image lock '{pair.Key}' is '{value}', expected '{pair.Value}'.");
			}

			foreach (var name in actual.Keys)
			{
				if (!expected.ContainsKey(name))
					throw new ToolFailureException($"SQL Server image lock has unexpected property '{name}'.");
			}
		}

		return LockedRepository + "@" + LockedManifestDigest;
	}

	private static string ResolveProductRoot()
	{
		// A proof copy used as the working directory must win over a script path that still sits in the live repo.
		var cwd = Directory.GetCurrentDirectory();
		if (LooksLikeProductRoot(cwd))
			return cwd;

		var scriptDir = Path.GetDirectoryName(Path.GetFullPath(Environment.GetCommandLineArgs()[0])) ??
						Directory.GetCurrentDirectory();

		foreach (var start in new[] { scriptDir, Directory.GetCurrentDirectory() })
		{
			for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
			{
				if (LooksLikeProductRoot(dir.FullName))
					return dir.FullName;

				var nested = Path.Combine(dir.FullName, "monaco");
				if (LooksLikeProductRoot(nested))
					return nested;
			}
		}

		throw new InvalidOperationException("Could not resolve the Monaco product root (expected .config/dotnet-tools.json and Directory.Packages.props).");
	}

	private static bool LooksLikeProductRoot(string path) =>
		File.Exists(Path.Combine(path, RelativeManifest)) &&
		File.Exists(Path.Combine(path, RelativeProps));

	private static string? ReadDesignPackageVersion(string propsPath)
	{
		var document = XDocument.Load(propsPath);
		foreach (var packageVersion in document.Descendants("PackageVersion"))
		{
			var include = packageVersion.Attribute("Include")?.Value;
			if (!string.Equals(include, DesignPackageId, StringComparison.Ordinal))
				continue;

			var version = packageVersion.Attribute("Version")?.Value;
			return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
		}

		return null;
	}

	private static string ReadSoleDotnetEfVersion(string manifestPath)
	{
		using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
		if (!document.RootElement.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException("Manifest has no tools object.");

		string? version = null;
		foreach (var tool in tools.EnumerateObject())
		{
			if (!string.Equals(tool.Name, ToolName, StringComparison.Ordinal))
				throw new InvalidDataException($"Manifest declares unexpected tool '{tool.Name}'.");

			if (!tool.Value.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String)
				throw new InvalidDataException($"Manifest tool '{ToolName}' has no string version.");

			version = versionElement.GetString();
		}

		if (version is null)
			throw new InvalidDataException($"Manifest does not declare '{ToolName}'.");

		if (string.IsNullOrWhiteSpace(version))
			throw new InvalidDataException($"Manifest tool '{ToolName}' has an empty version.");

		return version.Trim();
	}

	private static bool TryRestoreLocalTools(string productRoot, string manifestPath, out string error)
	{
		var startInfo = new ProcessStartInfo("dotnet")
		{
			WorkingDirectory = productRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add("tool");
		startInfo.ArgumentList.Add("restore");
		startInfo.ArgumentList.Add("--tool-manifest");
		startInfo.ArgumentList.Add(manifestPath);

		using var process = Process.Start(startInfo);
		if (process is null)
		{
			error = "Failed to start 'dotnet tool restore'.";
			return false;
		}

		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			error = $"dotnet tool restore failed (exit {process.ExitCode}).{Environment.NewLine}{stdout}{stderr}";
			return false;
		}

		error = string.Empty;
		return true;
	}

	private static bool TryReadEffectiveToolVersion(string manifestPath, out string version, out string error)
	{
		version = string.Empty;
		var startInfo = new ProcessStartInfo("dotnet")
		{
			// `dotnet tool run` resolves only a local manifest by walking from the working directory.
			WorkingDirectory = DirectoryOwningManifest(manifestPath),
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add("tool");
		startInfo.ArgumentList.Add("run");
		startInfo.ArgumentList.Add(ToolName);
		startInfo.ArgumentList.Add("--");
		startInfo.ArgumentList.Add("--version");

		using var process = Process.Start(startInfo);
		if (process is null)
		{
			error = $"Failed to start 'dotnet tool run {ToolName}'.";
			return false;
		}

		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			error = $"dotnet tool run {ToolName} --version failed (exit {process.ExitCode}).{Environment.NewLine}{stdout}{stderr}";
			return false;
		}

		var match = Regex.Match(stdout + Environment.NewLine + stderr, @"^\s*(\d+\.\d+\.\d+(?:[-+][\w.]+)?)\s*$",
			RegexOptions.Multiline);
		if (!match.Success)
		{
			error = $"Could not parse an exact version token from {ToolName} --version output.{Environment.NewLine}{stdout}{stderr}";
			return false;
		}

		version = match.Groups[1].Value;
		error = string.Empty;
		return true;
	}

	private static string DirectoryOwningManifest(string manifestPath)
	{
		var full = Path.GetFullPath(manifestPath);
		var parent = Path.GetDirectoryName(full);
		if (parent is not null && string.Equals(Path.GetFileName(parent), ".config", StringComparison.OrdinalIgnoreCase))
		{
			var owner = Path.GetDirectoryName(parent);
			if (!string.IsNullOrEmpty(owner))
				return owner;
		}

		return parent ?? Directory.GetCurrentDirectory();
	}

	private static string CreateTempRoot(string productRoot)
	{
		var root = Path.Combine(Path.GetTempPath(), "monaco-migrations-" + Guid.NewGuid().ToString("N"));
		var full = Path.GetFullPath(root);
		var product = Path.GetFullPath(productRoot);
		if (full.StartsWith(product + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
			full.Equals(product, StringComparison.OrdinalIgnoreCase))
			throw new ToolFailureException($"Temporary root {full} is inside the product root.");

		Directory.CreateDirectory(full);
		return full;
	}

	private static string? Cleanup(string? container, string? tempRoot, string? productRoot, string? secret)
	{
		var errors = new List<string>();
		if (container is not null)
		{
			try
			{
				var removed = Run("docker",
								  ["rm", "-f", container],
								  productRoot ?? Directory.GetCurrentDirectory(),
								  null,
								  "docker rm",
								  secret,
								  allowNonZero: true,
								  timeoutMilliseconds: 60_000);
				if (removed.ExitCode != 0 &&
					!removed.Stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase) &&
					!removed.Stdout.Contains("No such container", StringComparison.OrdinalIgnoreCase))
					throw new ToolFailureException($"docker rm failed (exit {removed.ExitCode}).{Environment.NewLine}{removed.Stdout}{removed.Stderr}");
			}
			catch (Exception ex)
			{
				errors.Add(ex.Message);
			}
		}

		if (tempRoot is not null)
		{
			try
			{
				DeleteTree(tempRoot);
			}
			catch (Exception ex)
			{
				errors.Add($"Could not delete {tempRoot}: {ex.Message}");
			}
		}

		return errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
	}

	private static void DeleteTree(string path)
	{
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				if (!Directory.Exists(path))
					return;

				foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
					File.SetAttributes(file, FileAttributes.Normal);
				Directory.Delete(path, recursive: true);
				return;
			}
			catch (Exception) when (attempt < 10)
			{
				Thread.Sleep(1000);
			}
		}
	}

	private static string? SafeProductRoot()
	{
		try
		{
			return ResolveProductRoot();
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static CommandResult Run(string fileName,
									 IReadOnlyList<string> arguments,
									 string workingDirectory,
									 IReadOnlyDictionary<string, string>? environment,
									 string display,
									 string? secret,
									 bool allowNonZero = false,
									 int? timeoutMilliseconds = null)
	{
		var startInfo = new ProcessStartInfo(fileName)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		foreach (var argument in arguments)
			startInfo.ArgumentList.Add(argument);
		if (environment is not null)
		{
			foreach (var pair in environment)
				startInfo.Environment[pair.Key] = pair.Value;
		}

		var result = Wait(startInfo, display, secret, timeoutMilliseconds);
		if (!allowNonZero && result.ExitCode != 0)
			throw new ToolFailureException($"{display} failed (exit {result.ExitCode}).{Environment.NewLine}{result.Stdout}{result.Stderr}");

		return result;
	}

	private static CommandResult Wait(ProcessStartInfo startInfo, string display, string? secret, int? timeoutMilliseconds = null)
	{
		var timeout = timeoutMilliseconds ?? CommandTimeoutMilliseconds;
		Process process;
		try
		{
			process = Process.Start(startInfo) ?? throw new ToolFailureException($"Failed to start {display}.");
		}
		catch (ToolFailureException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new ToolFailureException($"Failed to start {display}: {ex.Message}");
		}

		using (process)
		{
			var stdoutTask = process.StandardOutput.ReadToEndAsync();
			var stderrTask = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(timeout))
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch (Exception)
				{
				}

				process.WaitForExit(10_000);
				throw new ToolFailureException($"{display} did not finish within {timeout / 1000} seconds.");
			}

			if (!Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(30)))
				throw new ToolFailureException($"{display} exited but stdout/stderr drain timed out.");

			return new CommandResult(process.ExitCode, Scrub(stdoutTask.Result, secret), Scrub(stderrTask.Result, secret));
		}
	}

	private static string Scrub(string? text, string? secret)
	{
		if (string.IsNullOrEmpty(text))
			return text ?? "";

		if (!string.IsNullOrEmpty(secret))
			text = text.Replace(secret, "***", StringComparison.Ordinal);
		text = text.Replace("Password=unused", "Password=***", StringComparison.OrdinalIgnoreCase);
		return ConnectionStringPattern.Replace(text, "[redacted]");
	}

	private static string CreateSaPassword() =>
		"Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

	private static int FreeTcpPort()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}

	private static string ConnectionString(int port, string database, string password) =>
		$"Server=127.0.0.1,{port};Database={database};User ID=sa;Password={password};Encrypt=True;TrustServerCertificate=True;Connect Timeout=30;";

	private static string ProjectFile(string output, string name, string suffix) =>
		Path.Combine(output, $"{name}.{suffix}", $"{name}.{suffix}.csproj");

	private static string MigrationsDirectory(string output, string name) =>
		Path.Combine(output, $"{name}.Application", "Persistence", "Migrations");

	private static string FormatSet(IEnumerable<string> names) =>
		"[" + string.Join(", ", names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) + "]";

	private static void Step(string command, string message) =>
		Console.WriteLine($"migrations {command}: {message}");

	private static void Fail(string command, string message) =>
		Console.Error.WriteLine($"migrations {command} failed: {message}");

	private sealed record Fingerprint(string Name, bool Files, bool Messaging, string Database)
	{
		public string[] Tables()
		{
			var tables = new List<string> { "Country", "Company" };
			if (Files)
			{
				tables.Add("File");
				tables.Add("Product");
				tables.Add("ProductPicture");
			}

			if (Messaging)
			{
				tables.Add("InboxState");
				tables.Add("OutboxState");
				tables.Add("OutboxMessage");
			}

			return [.. tables];
		}

		public string Destination(string productRoot)
		{
			var solution = Path.Combine(productRoot, "src", "Content", "Backend", "Solution");
			if (Name == "Full")
				return Path.Combine(solution, "Monaco.Template.Backend.Application", "Persistence", "Migrations");

			return Path.Combine(solution, "_TemplateArtifacts", "MigrationVariants", Name);
		}
	}

	private sealed record SavedFile(string Path, byte[] Bytes);

	private sealed record TrioSnapshot(IReadOnlyList<string> Directories, IReadOnlyList<SavedFile> Files);

	private readonly record struct CommandResult(int ExitCode, string Stdout, string Stderr);

	private sealed class ToolFailureException : Exception
	{
		public ToolFailureException(string message) : base(message)
		{
		}
	}
}
