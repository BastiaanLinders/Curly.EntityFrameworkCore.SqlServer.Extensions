using Curly.EntityFrameworkCore.SqlServer.Extensions.ApplicationLock.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data;
using System.Data.Common;

namespace Curly.EntityFrameworkCore.SqlServer.Extensions.ApplicationLock.Implementation
{
    public class SqlApplicationLockProvider : IApplicationLockProvider
    {
        private const int SqlTimeoutExceptionNumber = -2;
        private readonly DbContext _context;
        
        public SqlApplicationLockProvider(DbContext context)
        {
            _context = context;
        }

        public IApplicationLock? AcquireLock(string resource)
        {
            throw new NotImplementedException();
        }

        public async Task<IApplicationLock?> AcquireLockAsync(string resource)
        {
            var currentTransaction = GetCurrentTransaction(_context);
            if (currentTransaction is null)
            {
                throw new Exception("Cannot start an application lock without a transaction. Make sure the context has a current transaction.");
            }

            using var command = _context.Database.GetDbConnection().CreateCommand();

            command.Transaction = currentTransaction;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "sp_getapplock";
            command.CommandTimeout = 1;
            command.Parameters.Add(new SqlParameter("Resource", resource));
            command.Parameters.Add(new SqlParameter("LockMode", "Exclusive"));
            command.Parameters.Add(new SqlParameter("LockOwner", "Session"));

            var returnValue = new SqlParameter() { Direction = ParameterDirection.ReturnValue };
            command.Parameters.Add(returnValue);

            try
            {
                await command.ExecuteNonQueryAsync();
                if ((int)returnValue.Value >= 0)
                {
                    return new ApplicationLock(_context, resource);
                }
                else
                {
                    return null;
                }
            }
            catch (SqlException exception) when (exception is { Number: SqlTimeoutExceptionNumber })
            {
                return null;
            }
        }

        private static DbTransaction? GetCurrentTransaction(DbContext context)
        {
            return (context.Database.CurrentTransaction as IInfrastructure<DbTransaction>)?.Instance;
        }

        public class ApplicationLock : IApplicationLock
        {
            private readonly DbContext _context;
            private readonly string _resource;

            public ApplicationLock(DbContext context, string resource)
            {
                _context = context;
                _resource = resource;
            }

            public void Release()
            {
                using var command = CreateReleaseCommand();
                var code = command.ExecuteNonQuery();
                if (code < 0)
                {
                    throw new Exception("Failed to release application lock");
                }
            }

            public async Task ReleaseAsync()
            {
                await using var command = CreateReleaseCommand();
                var code = await command.ExecuteNonQueryAsync();
                if (code < 0)
                {
                    throw new Exception("Failed to release application lock");
                }
            }

            private DbCommand CreateReleaseCommand()
            {
                var command = _context.Database.GetDbConnection().CreateCommand();

                command.Transaction = GetCurrentTransaction(_context)!;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "sp_releaseapplock";
                command.Parameters.Add(new SqlParameter("Resource", _resource));
                command.Parameters.Add(new SqlParameter("LockOwner", "Session"));

                return command;
            }

            public void Dispose()
            {
                ReleaseLock();
            }

            public async ValueTask DisposeAsync()
            {
                await ReleaseLockAsync();
            }
        }
    }

}