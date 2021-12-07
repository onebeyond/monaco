using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using DcslGs.Template.Common.Infrastructure.Context;

namespace DcslGs.Template.Infrastructure.Context;

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

    protected override Assembly GetConfigurationsAssembly()
    {
        return AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Single(x => x.GetName().Name.EndsWith(".Infrastructure") &&
                                     !x.GetName().Name.Contains(".Common."));
    }
}