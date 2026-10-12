using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Monaco.Eng.RuntimeShapeVerification.Infrastructure;

[ExcludeFromCodeCoverage]
internal static class ShapeAssemblyLoad
{
	private static readonly ConcurrentDictionary<string, byte> ProbeDirectories = new(StringComparer.OrdinalIgnoreCase);
	private static int _resolveHooked;

	internal static Assembly LoadHostAssembly(GeneratedShape shape, string suffix)
	{
		var dll = shape.BuiltDll(suffix);
		if (!File.Exists(dll))
			throw new FileNotFoundException($"Built host assembly not found for {shape.Id}/{suffix}.", dll);

		// Load into the default context so HostApplicationBuilder / IServiceCollection identity
		// matches the harness (required for configuration overrides and the Immediate(1) seam).
		var existing = AppDomain.CurrentDomain
								.GetAssemblies()
								.FirstOrDefault(assembly => assembly.Location.Equals(dll, StringComparison.OrdinalIgnoreCase));
		if (existing is not null)
			return existing;

		var dir = Path.GetDirectoryName(dll)!;
		ProbeDirectories[dir] = 0;
		if (Interlocked.Exchange(ref _resolveHooked, 1) == 0)
			AppDomain.CurrentDomain.AssemblyResolve += ResolveFromProbeDirectories;

		return Assembly.LoadFrom(dll);
	}

	private static Assembly? ResolveFromProbeDirectories(object? sender, ResolveEventArgs args)
	{
		var name = new AssemblyName(args.Name).Name;
		if (name is null)
			return null;

		return (ProbeDirectories.Keys
								.Select(dir => Path.Combine(dir, $"{name}.dll"))
								.Where(File.Exists)
								.Select(Assembly.LoadFrom))
			.FirstOrDefault();
	}
}

[ExcludeFromCodeCoverage]
internal static class GeneratedHostBootstrap
{
	internal static (IHost Host, IDisposable EnvironmentScope) Start(GeneratedShape shape,
																	  string suffix,
																	  IReadOnlyDictionary<string, string?> configuration,
																	  Action<IServiceCollection>? configureServices = null)
	{
		var assembly = ShapeAssemblyLoad.LoadHostAssembly(shape, suffix);

		// HostFactoryResolver may invoke the configure callback at HostBuilding (after Main already
		// bound connection strings). Seed env vars first so CreateApplicationBuilder sees them.
		var environmentScope = EnvironmentConfigurationScope.Apply(configuration);

		var configure = (object? hostBuilder) =>
						{
							if (hostBuilder is null)
								return;

							// Worker (and modern hosts) use Host.CreateApplicationBuilder → HostApplicationBuilder.
							if (hostBuilder is HostApplicationBuilder applicationBuilder)
							{
								// Do not mutate ApplicationName/ContentRoot — HostApplicationBuilder freezes them and throws.
								var projectDir = shape.ProjectDir(suffix);
								applicationBuilder.Configuration
												  .AddJsonFile(Path.Combine(projectDir, "appsettings.json"), optional: true, reloadOnChange: false)
												  .AddJsonFile(Path.Combine(projectDir, "appsettings.Development.json"), optional: true, reloadOnChange: false)
												  .AddInMemoryCollection(configuration);
								if (configureServices is not null)
									configureServices(applicationBuilder.Services);
								RemoveDuplicateWorkerBusHostedService(applicationBuilder.Services, shape.Options.MassTransit);
								return;
							}

							if (TryConfigureHostApplicationBuilder(hostBuilder, configuration, configureServices, shape.Options.MassTransit))
								return;

							if (hostBuilder is IHostBuilder typed)
							{
								typed.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
								typed.ConfigureServices((_, services) =>
														{
															configureServices?.Invoke(services);
															RemoveDuplicateWorkerBusHostedService(services, shape.Options.MassTransit);
														});
								return;
							}

							var configureWebHost = hostBuilder.GetType().GetMethod("ConfigureWebHost", [typeof(Action<IWebHostBuilder>)]);
							configureWebHost?.Invoke(hostBuilder,
													 [
														 (Action<IWebHostBuilder>)(web =>
																				   {
																					   web.ConfigureAppConfiguration((_, config) =>
																													 config.AddInMemoryCollection(configuration));
																					   if (configureServices is not null)
																						   web.ConfigureServices(configureServices);
																				   })
													 ]);
						};

		try
		{
			Console.WriteLine($"[RuntimeShapeVerification] Resolving host factory for {shape.Id}/{suffix}...");
			var hostFactory = ResolveHostFactory(assembly, configure)
							  ?? throw new InvalidOperationException($"Could not resolve host factory for {shape.Id}/{suffix}.");
			var args = BuildHostArguments(configuration);
			Console.WriteLine($"[RuntimeShapeVerification] Creating host {shape.Id}/{suffix}...");
			var host = hostFactory(args);
			Console.WriteLine($"[RuntimeShapeVerification] Starting host {shape.Id}/{suffix}...");
			host.Start();
			Console.WriteLine($"[RuntimeShapeVerification] Host started {shape.Id}/{suffix}.");
			return (host, environmentScope);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[RuntimeShapeVerification] Host start failed for {shape.Id}/{suffix}: {ex}");
			environmentScope.Dispose();
			throw;
		}
	}

	private static string[] BuildHostArguments(IReadOnlyDictionary<string, string?> configuration)
	{
		var args = new List<string> { "--environment=Development" };
		foreach (var pair in configuration)
		{
			if (pair.Value is null)
				continue;
			args.Add($"{pair.Key}={pair.Value}");
		}

		return [.. args];
	}

	private static Func<string[], IHost>? ResolveHostFactory(Assembly assembly, Action<object?> configureHostBuilder)
	{
		var resolverType = typeof(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<>).Assembly
																						   .GetType("Microsoft.Extensions.Hosting.HostFactoryResolver");
		if (resolverType is null)
			return null;

		var methods = resolverType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
								  .Where(method => method.Name == "ResolveHostFactory")
								  .ToArray();
		foreach (var method in methods)
		{
			var parameters = method.GetParameters();
			var args = new object?[parameters.Length];
			args[0] = assembly;
			for (var i = 1; i < parameters.Length; i++)
			{
				var parameter = parameters[i];
				if (parameter.ParameterType == typeof(bool))
					args[i] = false;
				else if (parameter.Name?.Contains("configure", StringComparison.OrdinalIgnoreCase) == true ||
						 parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition().Name.Contains("Action"))
					args[i] = configureHostBuilder;
				else if (parameter.HasDefaultValue)
					args[i] = parameter.DefaultValue;
				else if (!parameter.ParameterType.IsValueType)
					args[i] = null;
				else
					args[i] = Activator.CreateInstance(parameter.ParameterType);
			}

			try
			{
				switch (method.Invoke(null, args))
				{
					case Func<string[], IHost> typed:
						return typed;
					case Delegate del:
						return argv => (IHost)del.DynamicInvoke([argv])!;
				}
			}
			catch
			{
				// try next overload
			}
		}

		return null;
	}

	private static bool TryConfigureHostApplicationBuilder(object hostBuilder,
														   IReadOnlyDictionary<string, string?> configuration,
														   Action<IServiceCollection>? configureServices,
														   bool massTransit)
	{
		var type = hostBuilder.GetType();
		if (!string.Equals(type.Name, "HostApplicationBuilder", StringComparison.Ordinal))
			return false;

		var configurationProperty = type.GetProperty("Configuration")
									?? throw new InvalidOperationException("HostApplicationBuilder.Configuration was not found.");
		var servicesProperty = type.GetProperty("Services")
							   ?? throw new InvalidOperationException("HostApplicationBuilder.Services was not found.");

		var configurationManager = configurationProperty.GetValue(hostBuilder)
								   ?? throw new InvalidOperationException("HostApplicationBuilder.Configuration was null.");
		var services = (IServiceCollection)(servicesProperty.GetValue(hostBuilder)
											?? throw new InvalidOperationException("HostApplicationBuilder.Services was null."));

		var addInMemory = typeof(MemoryConfigurationBuilderExtensions)
						  .GetMethods(BindingFlags.Public | BindingFlags.Static)
						  .First(method => method.Name == nameof(MemoryConfigurationBuilderExtensions.AddInMemoryCollection) &&
										   method.GetParameters().Length == 2 &&
										   method.GetParameters()[1].ParameterType == typeof(IEnumerable<KeyValuePair<string, string?>>));
		addInMemory.Invoke(null, [configurationManager, configuration]);

		configureServices?.Invoke(services);
		RemoveDuplicateWorkerBusHostedService(services, massTransit);
		return true;
	}

	private static void RemoveDuplicateWorkerBusHostedService(IServiceCollection services, bool massTransit)
	{
		if (!massTransit)
			return;

		// Generated Worker.StartAsync and MassTransitHostedService both call IBusControl.StartAsync.
		// With concurrent hosted-service start they race into "receive endpoint was already started".
		services.Configure<HostOptions>(options => options.ServicesStartConcurrently = false);

		foreach (var descriptor in services.ToList()
										   .Where(IsHostedServiceDescriptor)
										   .Select(descriptor => new
																 {
																	 descriptor,
																	 implementationName = descriptor.ImplementationType?.Name ??
																						  descriptor.ImplementationInstance?.GetType().Name
																 })
										   .Select(t => new
														{
															t,
															fullName = t.descriptor.ImplementationType?.FullName ??
																	   t.descriptor.ImplementationInstance?.GetType().FullName ??
																	   string.Empty
														})
										   .Where(t => t.t.implementationName is "Worker" ||
													   t.fullName.EndsWith(".Worker.Worker", StringComparison.Ordinal))
										   .Select(t => t.t.descriptor))
			services.Remove(descriptor);
	}

	private static bool IsHostedServiceDescriptor(ServiceDescriptor descriptor) =>
		string.Equals(descriptor.ServiceType.FullName, typeof(IHostedService).FullName, StringComparison.Ordinal) ||
		string.Equals(descriptor.ServiceType.Name, nameof(IHostedService), StringComparison.Ordinal);
}

[ExcludeFromCodeCoverage]
internal sealed class EnvironmentConfigurationScope : IDisposable
{
	private readonly List<(string Key, string? Previous)> _previous = [];

	private EnvironmentConfigurationScope()
	{
	}

	internal static EnvironmentConfigurationScope Apply(IReadOnlyDictionary<string, string?> configuration)
	{
		var scope = new EnvironmentConfigurationScope();
		foreach (var pair in configuration)
		{
			if (pair.Value is null)
				continue;

			var key = pair.Key.Contains(':', StringComparison.Ordinal)
						  ? pair.Key.Replace(":", "__", StringComparison.Ordinal)
						  : pair.Key;
			scope._previous.Add((key, Environment.GetEnvironmentVariable(key)));
			Environment.SetEnvironmentVariable(key, pair.Value);
		}

		return scope;
	}

	public void Dispose()
	{
		foreach (var (key, previous) in _previous)
			Environment.SetEnvironmentVariable(key, previous);
	}
}

[ExcludeFromCodeCoverage]
internal static class HostConfiguration
{
	internal static Dictionary<string, string?> InfrastructureOverrides(GeneratedShape shape,
																		InfrastructureFixture infra,
																		string sqlConnectionString,
																		IReadOnlyDictionary<string, string?>? otlpOverrides = null)
	{
		var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
					 {
						 ["ConnectionStrings:AppDbContext"] = sqlConnectionString
					 };

		if (shape.Options.MassTransit)
		{
			values["MessageBus:RabbitMQ:Host"] = infra.RabbitMqHost;
			values["MessageBus:RabbitMQ:Port"] = infra.RabbitMqPort.ToString();
			values["MessageBus:RabbitMQ:Username"] = infra.RabbitMqUsername;
			values["MessageBus:RabbitMQ:Password"] = infra.RabbitMqPassword;
			values["MessageBus:RabbitMQ:VHost"] = InfrastructureFixture.RabbitMqVHost;
		}

		if (shape.Options.Files)
		{
			values["BlobStorage:ConnectionString"] = infra.StorageConnectionString;
			values["BlobStorage:Container"] = InfrastructureFixture.StorageContainer;
		}

		if (shape.Options.Auth)
		{
			values["SSO:Authority"] = infra.KeycloakRealmUrl;
			// Match the API and Keycloak audience client (Gateway appsettings still say monaco-template-backend).
			values["SSO:Audience"] = "monaco-template-api";
			values["SSO:RequireHttpsMetadata"] = "false";
		}

		if (otlpOverrides is not null)
			foreach (var pair in otlpOverrides)
				values[pair.Key] = pair.Value;

		return values;
	}
}

[ExcludeFromCodeCoverage]
internal sealed class ShapeHostSet : IAsyncDisposable
{
	private readonly List<IHost> _hosts = [];
	private readonly List<IDisposable> _environmentScopes = [];
	private readonly List<string> _processEnvironmentKeys = [];
	private readonly List<string> _hostDllPaths = [];
	private ProcessHost? _apiProcess;
	private ProcessHost? _gatewayProcess;
	private ProcessHost? _workerProcess;
	private int _disposed;

	public string? RetryMarkerPath { get; private set; }
	public string? StampMarkerPath { get; private set; }
	private string? _apiListenUrl;

	public IServiceProvider? WorkerServices { get; private set; }
	public HttpClient? ApiClient { get; private set; }
	public HttpClient? GatewayClient { get; private set; }
	public string? ApiBaseAddress { get; private set; }

	public static async Task<ShapeHostSet> StartAsync(GeneratedShape shape,
													  InfrastructureFixture infra,
													  string sqlConnectionString,
													  DestinationMode mode,
													  OtlpSignalReceiver? productionReceiver,
													  bool armConsumeRetry)
	{
		// Prior cells (or aborted startups) must not leave API/Gateway/Worker exporters alive.
		OrphanHostKiller.KillRuntimeShapeHosts();

		var set = new ShapeHostSet();
		try
		{
			var otlp = mode == DestinationMode.ProductionOverride
						   ? productionReceiver!.ProductionOverrideConfiguration(headers: "Authorization=Bearer runtime-shape-verify")
						   : null;
			var config = HostConfiguration.InfrastructureOverrides(shape,
																   infra,
																   sqlConnectionString,
																   otlp);
			// In-process hosts load configuration before the factory callback can append a source.
			// Environment variables are the source that still wins over the generated JSON.
			set.ApplyProcessEnvironment(config);
			if (mode != DestinationMode.ProductionOverride)
			{
				// Match checked-in Development OTLP. HostFactoryResolver keeps the test content root, so
				// generated appsettings.Development.json is not loaded unless we also inject these keys.
				set.SetProcessEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");
				set.SetProcessEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
			}

			if (shape.Options is { Api: true, Gateway: true })
			{
				// reverseProxy.json is AddJsonFile'd after env config in Gateway Program.cs, so cluster
				// Address overrides lose. Bind the API to the generated cluster destination instead.
				var apiUrl = shape.ClusterApiAddress;
				var apiDll = shape.BuiltDll("Api");
				set._hostDllPaths.Add(apiDll);
				set._apiProcess = await ProcessHost.StartAsync(apiDll,
															   shape.ProjectDir("Api"),
															   apiUrl,
															   config);
				set.ApiBaseAddress = apiUrl.TrimEnd('/') + "/";
				set.ApiClient = new() { BaseAddress = new(set.ApiBaseAddress) };
				await set._apiProcess.WaitForReadyAsync(new(new(set.ApiBaseAddress), "health"));

				var gatewayUrl = $"http://127.0.0.1:{GetFreeTcpPort()}";
				var gatewayConfig = new Dictionary<string, string?>(config, StringComparer.OrdinalIgnoreCase) { ["urls"] = gatewayUrl };
				var gatewayDll = shape.BuiltDll("Common.ApiGateway");
				set._hostDllPaths.Add(gatewayDll);
				set._gatewayProcess = await ProcessHost.StartAsync(gatewayDll,
																   shape.ProjectDir("Common.ApiGateway"),
																   gatewayUrl,
																   gatewayConfig);
				set.GatewayClient = new() { BaseAddress = new(gatewayUrl.TrimEnd('/') + "/") };
				// The generated Gateway has no health endpoint; YARP answers GET /health with 404 once it is listening.
				await set._gatewayProcess.WaitForReadyAsync(new(new(gatewayUrl.TrimEnd('/') + "/"), "health"),
															acceptAnyHttpResponse: true);
			}
			else if (shape.Options.Api)
			{
				set._apiListenUrl = $"http://127.0.0.1:{GetFreeTcpPort()}";
				var apiDll = shape.BuiltDll("Api");
				set._hostDllPaths.Add(apiDll);
				set._apiProcess = await ProcessHost.StartAsync(apiDll,
															   shape.ProjectDir("Api"),
															   set._apiListenUrl,
															   config);
				set.ApiBaseAddress = set._apiListenUrl.TrimEnd('/') + "/";
				set.ApiClient = new() { BaseAddress = new(set.ApiBaseAddress) };
				await set._apiProcess.WaitForReadyAsync(new(new(set.ApiBaseAddress), "health"));
			}

			if (shape.Options is { Worker: true, MassTransit: true })
			{
				// Messaging Workers run out-of-process. The startup hook removes the duplicate
				// Worker BackgroundService bus start and arms retry/stamp seams.
				var raw = new Dictionary<string, string?>(StringComparer.Ordinal)
						  {
							  ["DOTNET_STARTUP_HOOKS"] = DeployWorkerHook(shape), ["OTEL_SERVICE_NAME"] = shape.ServiceName("Worker"), ["RUNTIME_SHAPE_HOOK_LOG"] = Path.Combine(Path.GetTempPath(), $"hook-{shape.Id}.log")
						  };
				if (armConsumeRetry)
				{
					set.RetryMarkerPath = Path.Combine(Path.GetTempPath(), $"retry-{shape.Id}-{Guid.NewGuid():N}.txt");
					raw["RUNTIME_SHAPE_RETRY_MARKER"] = set.RetryMarkerPath;
				}

				if (shape.Shape == RuntimeShape.WorkerConsumer)
				{
					set.StampMarkerPath = Path.Combine(Path.GetTempPath(), $"stamp-{shape.Id}-{Guid.NewGuid():N}.txt");
					raw["RUNTIME_SHAPE_STAMP"] = "1";
					raw["RUNTIME_SHAPE_STAMP_MARKER"] = set.StampMarkerPath;
				}

				var workerDll = shape.BuiltDll("Worker");
				set._hostDllPaths.Add(workerDll);
				set._workerProcess = await ProcessHost.StartAsync(workerDll,
																  shape.ProjectDir("Worker"),
																  "http://127.0.0.1:0",
																  config,
																  raw);
				await set._workerProcess.WaitForOutputAsync("Bus started", TimeSpan.FromSeconds(90));
			}
			else if (shape.Options.Worker)
			{
				set.SetProcessEnvironment("OTEL_SERVICE_NAME", shape.ServiceName("Worker"));
				var (workerHost, workerEnv) = GeneratedHostBootstrap.Start(shape,
																		   "Worker",
																		   config);
				set._hosts.Add(workerHost);
				set._environmentScopes.Add(workerEnv);
				set.WorkerServices = workerHost.Services;
			}

			return set;
		}
		catch
		{
			await set.DisposeAsync();
			throw;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		ClearProcessEnvironment();
		foreach (var scope in _environmentScopes)
			scope.Dispose();
		_environmentScopes.Clear();
		ApiClient?.Dispose();
		GatewayClient?.Dispose();

		var tracked = new List<ProcessHost?> { _apiProcess, _gatewayProcess, _workerProcess };
		foreach (var host in tracked.OfType<ProcessHost>())
			await host.DisposeAsync();

		_apiProcess = null;
		_gatewayProcess = null;
		_workerProcess = null;

		for (var i = _hosts.Count - 1; i >= 0; i--)
		{
			try
			{
				await _hosts[i].StopAsync(TimeSpan.FromSeconds(5));
			}
			catch
			{
				// best-effort stop before dispose
			}

			_hosts[i].Dispose();
		}

		_hosts.Clear();

		if (_hostDllPaths.Count > 0)
			OrphanHostKiller.KillByDllPaths(_hostDllPaths);
		else
			OrphanHostKiller.KillRuntimeShapeHosts();
	}

	private void ApplyProcessEnvironment(IReadOnlyDictionary<string, string?> configuration)
	{
		SetProcessEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
		SetProcessEnvironment("DOTNET_ENVIRONMENT", "Development");
		foreach (var pair in configuration)
		{
			if (pair.Value is null)
				continue;

			var key = pair.Key.Contains(':', StringComparison.Ordinal)
						  ? pair.Key.Replace(":", "__", StringComparison.Ordinal)
						  : pair.Key;
			SetProcessEnvironment(key, pair.Value);
		}
	}

	private void SetProcessEnvironment(string key, string value)
	{
		Environment.SetEnvironmentVariable(key, value);
		_processEnvironmentKeys.Add(key);
	}

	private void ClearProcessEnvironment()
	{
		foreach (var key in _processEnvironmentKeys)
			Environment.SetEnvironmentVariable(key, null);

		_processEnvironmentKeys.Clear();
	}

	private static string DeployWorkerHook(GeneratedShape shape)
	{
		var source = Path.Combine(AppContext.BaseDirectory, "Monaco.Eng.RuntimeShapeVerification.WorkerHook.dll");
		if (!File.Exists(source))
			throw new FileNotFoundException("Worker startup hook was not copied to the test output.", source);

		// Load the hook from the Worker output directory so MassTransit/MediatR/Hosting
		// resolve to the Worker's assemblies instead of a second copy beside the testhost.
		var workerBin = Path.GetDirectoryName(shape.BuiltDll("Worker"))
						?? throw new InvalidOperationException("Worker output directory was not found.");
		var destination = Path.Combine(workerBin, "Monaco.Eng.RuntimeShapeVerification.WorkerHook.dll");
		File.Copy(source, destination, overwrite: true);
		return destination;
	}

	private static int GetFreeTcpPort()
	{
		var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
		listener.Start();
		var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}
}

[ExcludeFromCodeCoverage]
internal sealed class ProcessHost : IAsyncDisposable
{
	private readonly Process _process;
	private readonly StringBuilder _stdout = new();
	private readonly StringBuilder _stderr = new();

	private ProcessHost(Process process) =>
		_process = process;

	internal static async Task<ProcessHost> StartAsync(string dllPath,
													   string contentRoot,
													   string urls,
													   IReadOnlyDictionary<string, string?> environment,
													   IReadOnlyDictionary<string, string?>? rawEnvironment = null)
	{
		var startInfo = new ProcessStartInfo("dotnet", $"\"{dllPath}\"")
						{
							WorkingDirectory = contentRoot,
							RedirectStandardOutput = true,
							RedirectStandardError = true,
							UseShellExecute = false,
							CreateNoWindow = true,
							Environment =
							{
								["ASPNETCORE_URLS"] = urls,
								["ASPNETCORE_ENVIRONMENT"] = "Development",
								["DOTNET_ENVIRONMENT"] = "Development"
							}
						};
		// ProcessStartInfo inherits the parent environment; strip Worker-only OTEL resource stamps.
		startInfo.Environment.Remove("OTEL_SERVICE_NAME");
		foreach (var pair in environment)
		{
			if (pair.Value is null)
				continue;

			// Pass OTEL_EXPORTER_OTLP_* and flat keys as-is; map hierarchical config keys to env form.
			var key = pair.Key.Contains(':', StringComparison.Ordinal)
						  ? pair.Key.Replace(":", "__", StringComparison.Ordinal)
						  : pair.Key;
			startInfo.Environment[key] = pair.Value;
		}

		if (rawEnvironment is not null)
			foreach (var pair in rawEnvironment)
			{
				if (pair.Value is null)
					continue;

				startInfo.Environment[pair.Key] = pair.Value;
			}

		var host = new ProcessHost(Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {dllPath}"));
		host._process.OutputDataReceived += (_, eventArgs) =>
											{
												if (eventArgs.Data is not null)
													host._stdout.AppendLine(eventArgs.Data);
											};
		host._process.ErrorDataReceived += (_, eventArgs) =>
										   {
											   if (eventArgs.Data is not null)
												   host._stderr.AppendLine(eventArgs.Data);
										   };
		host._process.BeginOutputReadLine();
		host._process.BeginErrorReadLine();
		await Task.Delay(500);
		return host._process.HasExited
				   ? throw new InvalidOperationException($"Host exited early ({host._process.ExitCode}): {host._stdout} {host._stderr}")
				   : host;
	}

	internal async Task WaitForOutputAsync(string text, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (_process.HasExited)
				throw new InvalidOperationException($"Host process exited while waiting for '{text}' (code {_process.ExitCode}). Stderr: {_stderr} Stdout: {_stdout}");

			if (_stdout.ToString().Contains(text, StringComparison.Ordinal) ||
				_stderr.ToString().Contains(text, StringComparison.Ordinal))
				return;

			await Task.Delay(200);
		}

		throw new TimeoutException($"Did not observe '{text}'. Stderr: {_stderr} Stdout: {_stdout}");
	}

	internal async Task WaitForReadyAsync(Uri healthUrl, bool acceptAnyHttpResponse = false)
	{
		using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
		for (var attempt = 0; attempt < 90; attempt++)
		{
			if (_process.HasExited)
				throw new InvalidOperationException($"Host process exited while waiting for readiness (code {_process.ExitCode}). Stderr: {_stderr} Stdout: {_stdout}");

			try
			{
				using var response = await client.GetAsync(healthUrl);
				if (response.IsSuccessStatusCode || acceptAnyHttpResponse)
					return;
			}
			catch
			{
				// not ready
			}

			await Task.Delay(250);
		}

		throw new TimeoutException($"Process host did not become ready at {healthUrl}. Stderr: {_stderr} Stdout: {_stdout}");
	}

	public async ValueTask DisposeAsync()
	{
		var pid = 0;
		try
		{
			pid = _process.Id;
		}
		catch
		{
			// process already disposed
		}

		try
		{
			if (!_process.HasExited)
			{
				_process.Kill(entireProcessTree: true);
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
				try
				{
					await _process.WaitForExitAsync(cts.Token);
				}
				catch (OperationCanceledException)
				{
					OrphanHostKiller.KillPidTree(pid);
				}
			}
		}
		catch
		{
			if (pid > 0)
				OrphanHostKiller.KillPidTree(pid);
		}
		finally
		{
			_process.Dispose();
		}
	}
}

[ExcludeFromCodeCoverage]
internal static class OrphanHostKiller
{
	internal static void KillRuntimeShapeHosts() =>
		KillMatching(IsGeneratedShapeHostCommandLine);

	internal static void KillByDllPaths(IEnumerable<string> dllPaths)
	{
		var fragments = dllPaths
						.Where(path => !string.IsNullOrWhiteSpace(path))
						.Select(path => Path.GetFileName(path))
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.ToArray();
		if (fragments.Length == 0)
		{
			KillRuntimeShapeHosts();
			return;
		}

		KillMatching(commandLine => fragments.Any(fragment => commandLine.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
	}

	internal static void KillPidTree(int pid)
	{
		if (pid <= 0)
			return;

		try
		{
			using var process = Process.Start(new ProcessStartInfo
											  {
												  FileName = "taskkill",
												  Arguments = $"/PID {pid} /T /F",
												  CreateNoWindow = true,
												  UseShellExecute = false,
												  RedirectStandardOutput = true,
												  RedirectStandardError = true
											  });
			process?.WaitForExit(5_000);
		}
		catch
		{
			// best-effort cleanup
		}
	}

	private static bool IsGeneratedShapeHostCommandLine(string commandLine) =>
		commandLine.Contains("monaco-runtime-shape-verify", StringComparison.OrdinalIgnoreCase) &&
		(commandLine.Contains(".Api.dll", StringComparison.OrdinalIgnoreCase) ||
		 commandLine.Contains(".Worker.dll", StringComparison.OrdinalIgnoreCase) ||
		 commandLine.Contains(".Common.ApiGateway.dll", StringComparison.OrdinalIgnoreCase) ||
		 commandLine.Contains("RuntimeR", StringComparison.OrdinalIgnoreCase));

	private static void KillMatching(Func<string, bool> match)
	{
		var self = Environment.ProcessId;
		foreach (var (pid, commandLine) in EnumerateProcessCommandLines())
		{
			if (pid == self || string.IsNullOrEmpty(commandLine))
				continue;

			if (!match(commandLine))
				continue;

			// Never kill the verification suite itself.
			if (commandLine.Contains("RuntimeShapeVerification.dll", StringComparison.OrdinalIgnoreCase) ||
				commandLine.Contains("testhost", StringComparison.OrdinalIgnoreCase) ||
				commandLine.Contains("RuntimeShapeVerification.csproj", StringComparison.OrdinalIgnoreCase))
				continue;

			try
			{
				using var process = Process.GetProcessById(pid);
				process.Kill(entireProcessTree: true);
			}
			catch
			{
				KillPidTree(pid);
			}
		}
	}

	private static IEnumerable<(int Pid, string CommandLine)> EnumerateProcessCommandLines()
	{
		if (!OperatingSystem.IsWindows())
			yield break;

		Process? process = null;
		try
		{
			process = Process.Start(new ProcessStartInfo
									{
										FileName = "powershell",
										Arguments = "-NoProfile -Command \"Get-CimInstance Win32_Process | Select-Object ProcessId, CommandLine | ConvertTo-Csv -NoTypeInformation\"",
										CreateNoWindow = true,
										UseShellExecute = false,
										RedirectStandardOutput = true,
										RedirectStandardError = true
									});
		}
		catch
		{
			yield break;
		}

		if (process is null)
			yield break;

		using (process)
		{
			var headerSkipped = false;
			while (process.StandardOutput.ReadLine() is { } line)
			{
				if (!headerSkipped)
				{
					headerSkipped = true;
					continue;
				}

				if (!TryParseCsvProcessRow(line, out var pid, out var commandLine))
					continue;

				yield return (pid, commandLine);
			}

			process.WaitForExit(5_000);
		}
	}

	private static bool TryParseCsvProcessRow(string line, out int pid, out string commandLine)
	{
		pid = 0;
		commandLine = string.Empty;
		if (string.IsNullOrWhiteSpace(line))
			return false;

		// "ProcessId","CommandLine" — CommandLine may contain commas and quotes.
		if (!line.StartsWith('"'))
			return false;

		var secondQuote = line.IndexOf('"', 1);
		if (secondQuote <= 1)
			return false;

		var pidText = line[1..secondQuote];
		if (!int.TryParse(pidText, out pid))
			return false;

		var remainder = line[(secondQuote + 1)..];
		if (!remainder.StartsWith(','))
			return false;

		remainder = remainder[1..];
		if (remainder.StartsWith('"') && remainder.EndsWith('"'))
			commandLine = remainder[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
		else
			commandLine = remainder;

		return true;
	}
}

[ExcludeFromCodeCoverage]
internal static class AuthTokenHelper
{
	internal static async Task<string?> TryGetAdministratorTokenAsync(InfrastructureFixture infra, GeneratedShape shape)
	{
		if (!shape.Options.Auth)
			return null;

		const string realm = InfrastructureFixture.KeycloakRealm;
		var tokenEndpoint = $"{infra.KeycloakBaseAddress.TrimEnd('/')}/realms/{realm}/protocol/openid-connect/token";
		var admin = new Keycloak.Net.KeycloakClient(infra.KeycloakBaseAddress,
													InfrastructureFixture.KeycloakRealmUsername,
													InfrastructureFixture.KeycloakRealmPassword);
		var clientId = Guid.NewGuid().ToString("N");
		var secret = Guid.NewGuid().ToString("N");
		var scopes = new[]
					 {
						 "companies:read",
						 "companies:write",
						 "files:write",
						 "products:write",
						 "email",
						 "profile",
						 "roles"
					 };
		await admin.CreateClientAsync(realm,
									  new()
									  {
										  Name = "RuntimeShapeVerification",
										  ClientId = clientId,
										  Secret = secret,
										  ServiceAccountsEnabled = true,
										  StandardFlowEnabled = false,
										  AuthorizationServicesEnabled = false,
										  DefaultClientScopes = ["monaco-template-api", .. scopes]
									  });

		var created = (await admin.GetClientsAsync(realm, clientId)).Single();
		var serviceUser = await admin.GetUserForServiceAccountAsync(realm, created.Id);
		var audience = (await admin.GetClientsAsync(realm, "monaco-template-api")).Single();
		var roles = await admin.GetRolesAsync(realm, audience.Id);
		await admin.AddClientRoleMappingsToUserAsync(realm,
													 serviceUser.Id,
													 audience.Id,
													 [.. roles.Where(role => role.Name is "Administrator")]);

		using var http = new HttpClient();
		using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = clientId, ["client_secret"] = secret, ["grant_type"] = "client_credentials" });
		using var response = await http.PostAsync(tokenEndpoint, content);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync();
		using var document = await JsonDocument.ParseAsync(stream);
		return document.RootElement.GetProperty("access_token").GetString();
	}

	internal static void AttachBearer(HttpClient client, string? token)
	{
		if (token is null)
			return;

		client.DefaultRequestHeaders.Authorization = new("Bearer", token);
	}
}