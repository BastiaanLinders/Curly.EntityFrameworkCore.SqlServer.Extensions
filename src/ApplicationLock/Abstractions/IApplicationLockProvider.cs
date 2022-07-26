namespace Curly.EntityFrameworkCore.SqlServer.Extensions.ApplicationLock.Abstractions
{
    public interface IApplicationLockProvider
    {
        IApplicationLock? AcquireLock(string resource);
        Task<IApplicationLock?> AcquireLockAsync(string resource);
    }
}