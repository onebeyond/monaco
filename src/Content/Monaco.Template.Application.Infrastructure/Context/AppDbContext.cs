using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Common.Infrastructure.Context;
using System.Reflection;

namespace Monaco.Template.Application.Infrastructure.Context;

public class AppDbContext : BaseDbContext
{
    protected AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options,
                        IMediator mediator,
                        IHostEnvironment env) : base(options, mediator, env)
    {
    }

	protected override Assembly GetConfigurationsAssembly() =>
		Assembly.GetAssembly(typeof(AppDbContext))!;
}