using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monaco.Eng.RuntimeShapeVerification.WorkerHook;
using System.Diagnostics;
using System.Reflection;

public static class StartupHook
{
	public static void Initialize()
	{
		try
		{
			File.WriteAllText(Path.Combine(Path.GetTempPath(), "rsv-hook-init.txt"), $"{DateTime.UtcNow:O} pid={Environment.ProcessId}");
		}
		catch
		{
			// best-effort
		}

		Log("initialize");
		DiagnosticListener.AllListeners.Subscribe(new HostingListener());
	}

	private static void Log(string message)
	{
		var path = Environment.GetEnvironmentVariable("RUNTIME_SHAPE_HOOK_LOG");
		if (string.IsNullOrEmpty(path))
			return;

		File.AppendAllText(path, message + Environment.NewLine);
	}

	private sealed class HostingListener : IObserver<DiagnosticListener>
	{
		public void OnCompleted()
		{
		}

		public void OnError(Exception error)
		{
		}

		public void OnNext(DiagnosticListener value)
		{
			if (value.Name == "Microsoft.Extensions.Hosting")
				value.Subscribe(new HostBuildingObserver());
		}
	}

	private sealed class HostBuildingObserver : IObserver<KeyValuePair<string, object?>>
	{
		public void OnCompleted()
		{
		}

		public void OnError(Exception error)
		{
		}

		public void OnNext(KeyValuePair<string, object?> value)
		{
			Log($"event {value.Key} {value.Value?.GetType().FullName}");
			if (value.Key != "HostBuilding" || value.Value is null)
				return;

			var builder = value.Value;
			var services = builder.GetType().GetProperty("Services")?.GetValue(builder);
			if (services is not IServiceCollection collection)
			{
				var configure = builder.GetType()
									   .GetMethod("ConfigureServices",
												  [typeof(Action<HostBuilderContext, IServiceCollection>)]);
				Log($"configure {configure}");
				if (configure is null)
					return;

				var parameterType = configure.GetParameters()[0].ParameterType;
				var add = typeof(HostBuildingObserver).GetMethod(nameof(AddWorkerServices), BindingFlags.NonPublic | BindingFlags.Static);
				try
				{
					var callback = Delegate.CreateDelegate(parameterType, add!);
					configure.Invoke(builder, [callback]);
					Log("configure-invoked");
				}
				catch (Exception ex)
				{
					Log($"configure-failed {ex.GetType().Name}: {ex.Message}");
				}

				return;
			}

			Log("services-matched");

			var marker = Environment.GetEnvironmentVariable("RUNTIME_SHAPE_RETRY_MARKER");
			if (!string.IsNullOrEmpty(marker))
			{
				collection.AddSingleton(new OneShotFailure(marker));
				collection.AddSingleton<IConfigureReceiveEndpoint, OneShotRetryConfiguration>();
				collection.AddTransient(typeof(IPipelineBehavior<,>), typeof(OneShotBehavior<,>));
			}

			if (string.Equals(Environment.GetEnvironmentVariable("RUNTIME_SHAPE_STAMP"),
							  "1",
							  StringComparison.Ordinal))
				collection.AddHostedService<FixtureStampService>();
		}

		private static void AddWorkerServices(HostBuilderContext _, IServiceCollection collection)
		{
			Log("add-worker-services");

			// Same race as the in-process harness: Worker.StartAsync and MassTransitHostedService
			// both call IBusControl.StartAsync. Keep MassTransit; drop the BackgroundService Worker.
			collection.Configure<HostOptions>(options => options.ServicesStartConcurrently = false);
			foreach (var descriptor in collection.ToList()
												 .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
												 .Select(descriptor => new
																	   {
																		   descriptor,
																		   name = descriptor.ImplementationType?.Name ??
																				  descriptor.ImplementationInstance?.GetType().Name
																	   })
												 .Select(t => new { t, fullName = t.descriptor.ImplementationType?.FullName ?? string.Empty })
												 .Where(t => t.t.name is "Worker" ||
															 t.fullName.EndsWith(".Worker.Worker", StringComparison.Ordinal))
												 .Select(t => t.t.descriptor))
			{
				collection.Remove(descriptor);
				Log("removed-worker-hosted-service");
			}

			var marker = Environment.GetEnvironmentVariable("RUNTIME_SHAPE_RETRY_MARKER");
			if (!string.IsNullOrEmpty(marker))
			{
				collection.AddSingleton(new OneShotFailure(marker));
				collection.AddSingleton<IConfigureReceiveEndpoint, OneShotRetryConfiguration>();
				collection.AddTransient(typeof(IPipelineBehavior<,>), typeof(OneShotBehavior<,>));
				Log("retry-armed");
			}

			if (string.Equals(Environment.GetEnvironmentVariable("RUNTIME_SHAPE_STAMP"),
							  "1",
							  StringComparison.Ordinal))
			{
				collection.AddHostedService<FixtureStampService>();
				Log("stamp-armed");
			}
		}
	}

	private sealed class OneShotFailure
	{
		private readonly string _markerPath;
		private int _thrown;

		public OneShotFailure(string markerPath) =>
			_markerPath = markerPath;

		public bool TryThrow(Guid? messageId, out Exception? exception)
		{
			exception = null;
			if (Interlocked.CompareExchange(ref _thrown,
											1,
											0) !=
				0)
				return false;

			File.WriteAllText(_markerPath, messageId?.ToString("D") ?? "thrown");
			exception = new TransientConsumeException("Transient consume failure.");
			return true;
		}
	}

	private sealed class TransientConsumeException : Exception
	{
		public TransientConsumeException(string message) : base(message)
		{
		}
	}

	private sealed class OneShotRetryConfiguration : IConfigureReceiveEndpoint
	{
		public void Configure(string name, IReceiveEndpointConfigurator configurator)
		{
			if (name.Contains("_bus_", StringComparison.OrdinalIgnoreCase))
				return;

			configurator.UseMessageRetry(retry =>
										 {
											 retry.Immediate(1);
											 retry.Handle<TransientConsumeException>();
										 });
		}
	}

	private sealed class OneShotBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		where TRequest : notnull
	{
		private readonly IServiceProvider _services;
		private readonly OneShotFailure _failure;

		public OneShotBehavior(IServiceProvider services, OneShotFailure failure)
		{
			_services = services;
			_failure = failure;
		}

		public Task<TResponse> Handle(TRequest request,
									  RequestHandlerDelegate<TResponse> next,
									  CancellationToken cancellationToken)
		{
			var requestName = typeof(TRequest).FullName ?? typeof(TRequest).Name;
			if (!requestName.Contains("LongRunningProcess", StringComparison.Ordinal))
				return next(cancellationToken);

			// ConsumeContext may be absent depending on MediatR scope; still force one throw + marker.
			var consumeContext = _services.GetService<ConsumeContext>();
			var messageId = consumeContext?.MessageId ?? Guid.NewGuid();
			return _failure.TryThrow(messageId, out var exception)
					   ? throw exception!
					   : next(cancellationToken);
		}
	}

	private sealed class FixtureStampService : IHostedService
	{
		private readonly IServiceProvider _services;

		public FixtureStampService(IServiceProvider services) =>
			_services = services;

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			using var scope = _services.CreateScope();
			var id = await GeneratedCompanyStamp.SaveAsync(scope.ServiceProvider, cancellationToken);
			var marker = Environment.GetEnvironmentVariable("RUNTIME_SHAPE_STAMP_MARKER");
			if (!string.IsNullOrEmpty(marker))
				await File.WriteAllTextAsync(marker, id.ToString(), cancellationToken);
		}

		public Task StopAsync(CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}
}