namespace Demo.Application.Common;

/// <summary>Commits every change tracked in the current scope atomically. Repositories never save on their own.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
