using System.Reflection;
#if massTransitIntegration
using MassTransit;
#endif
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Backend.Common.Infrastructure.Context;

namespace Monaco.Template.Backend.Application.Persistence;

public class AppDbContext : BaseDbContext
{
	protected AppDbContext()
	{
	}

	public AppDbContext(DbContextOptions<AppDbContext> options,
						IPublisher publisher,
						IHostEnvironment env) : base(options, publisher, env)
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