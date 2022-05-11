using System.Reflection;
using DcslGs.Template.Common.Infrastructure.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DcslGs.Template.Application.Infrastructure.Context;

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