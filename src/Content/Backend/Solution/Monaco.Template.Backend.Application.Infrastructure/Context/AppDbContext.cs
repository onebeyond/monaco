using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using System.Reflection;

namespace Monaco.Template.Backend.Application.Infrastructure.Context;

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

	protected override Assembly GetConfigurationsAssembly() =>
		Assembly.GetAssembly(typeof(AppDbContext))!;
}