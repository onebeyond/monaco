using System.Reflection;
#if massTransitIntegration
using MassTransit;
#endif
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Serilog;

namespace Monaco.Template.Backend.Application.Persistence;

public class AppDbContext : BaseDbContext
{
	protected AppDbContext()
	{
	}

	public AppDbContext(DbContextOptions<AppDbContext> options,
						IPublisher publisher,
						IHostEnvironment env,
						ILogger auditLogger) : base(options, publisher, env, auditLogger)
	{
	}
#if massTransitIntegration

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		
		modelBuilder.AddTransactionalOutboxEntities();
	}
#endif

	protected override Assembly GetConfigurationsAssembly() =>
		Assembly.GetAssembly(typeof(AppDbContext))!;
}