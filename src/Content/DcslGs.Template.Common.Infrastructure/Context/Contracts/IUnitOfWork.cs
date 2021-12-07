namespace DcslGs.Template.Common.Infrastructure.Context.Contracts;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken);
}