namespace Curly.EntityFrameworkCore.SqlServer.Extensions.ApplicationLock.Abstractions
{
    public interface IApplicationLock : IDisposable, IAsyncDisposable
    {
        Task ReleaseAsync();
        void Release();
    }
}