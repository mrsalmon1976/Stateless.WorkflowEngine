using System.Data;

namespace Example.Shared
{
    /// <summary>
    /// The volume test database, registered as a scoped service.  Each workflow action runs in its own scope, so an
    /// action gets its own connection, which is closed when the scope is disposed after the action has executed.
    /// </summary>
    public class VolumeDb : IDisposable
    {
        private readonly ExampleDbType _dbType;
        private IDbConnection? _connection;

        public VolumeDb(ExampleDbType dbType)
        {
            _dbType = dbType;
        }

        /// <summary>
        /// Gets the open connection, opening it on first use.
        /// </summary>
        public IDbConnection Connection => _connection ??= DbHelper.GetConnection(_dbType);

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
