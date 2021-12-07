using System.Linq.Expressions;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using DcslGs.Template.Common.Domain.Model;

namespace DcslGs.Template.Common.Infrastructure.Context.Extensions;

public static class OperationsExtensions
{
    public static async Task<bool> ExistsAsync<T>(this DbContext dbContext, Guid id, CancellationToken cancellationToken = default) where T : Entity
    {
        return await dbContext.Set<T>().AsExpandable().AnyAsync(x => x.Id == id, cancellationToken);
    }

    public static async Task<bool> ExistsAsync<T>(this DbContext dbContext, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : Entity
    {
        return await dbContext.Set<T>().AsExpandable().AnyAsync(predicate, cancellationToken);
    }
}