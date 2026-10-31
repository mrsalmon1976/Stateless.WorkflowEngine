using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Security;
using System.IO.Abstractions;

namespace Stateless.WorkflowEngine.WebConsole.Storage.Stores
{
    /// <summary>
    /// File-based store for the users and connections of the console.  The file format is shared with
    /// previous versions of the console so existing installs can be upgraded in place.
    /// </summary>
    public interface IUserStore
    {
        string FilePath { get; }

        IReadOnlyList<UserModel> Users { get; }

        IReadOnlyList<ConnectionModel> Connections { get; }

        ConnectionModel? GetConnection(Guid connectionId);

        UserModel? GetUser(Guid userId);

        UserModel? GetUser(string userName);

        /// <summary>
        /// Loads the users and connections from disk, creating a default admin user on first run and
        /// migrating any legacy connection passwords.
        /// </summary>
        void Load();

        /// <summary>
        /// Adds or replaces (by Id) a connection and saves the store.
        /// </summary>
        void SaveConnection(ConnectionModel connection);

        /// <summary>
        /// Adds or replaces (by Id) a user and saves the store.
        /// </summary>
        void SaveUser(UserModel user);

        void DeleteConnection(Guid connectionId);

        void DeleteUser(Guid userId);
    }

    public class UserStore : IUserStore
    {
        private readonly IFileSystem _fileSystem;
        private readonly IPasswordProvider _passwordProvider;
        private readonly IConnectionPasswordProtector _connectionPasswordProtector;
        private readonly ILogger<UserStore> _logger;
        private readonly object _syncRoot = new object();
        private List<UserModel> _users = new List<UserModel>();
        private List<ConnectionModel> _connections = new List<ConnectionModel>();

        public UserStore(string filePath, IFileSystem fileSystem, IPasswordProvider passwordProvider, IConnectionPasswordProtector connectionPasswordProtector, ILogger<UserStore> logger)
        {
            FilePath = filePath;
            _fileSystem = fileSystem;
            _passwordProvider = passwordProvider;
            _connectionPasswordProtector = connectionPasswordProtector;
            _logger = logger;
        }

        public string FilePath { get; }

        public IReadOnlyList<UserModel> Users
        {
            get { lock (_syncRoot) { return _users.ToList(); } }
        }

        public IReadOnlyList<ConnectionModel> Connections
        {
            get { lock (_syncRoot) { return _connections.ToList(); } }
        }

        public ConnectionModel? GetConnection(Guid connectionId)
        {
            return Connections.SingleOrDefault(x => x.Id == connectionId);
        }

        public UserModel? GetUser(Guid userId)
        {
            return Users.SingleOrDefault(x => x.Id == userId);
        }

        public UserModel? GetUser(string userName)
        {
            return Users.FirstOrDefault(x => String.Equals(x.UserName, userName, StringComparison.OrdinalIgnoreCase));
        }

        public void Load()
        {
            lock (_syncRoot)
            {
                if (_fileSystem.File.Exists(FilePath))
                {
                    var data = JsonConvert.DeserializeObject<UserStoreData>(_fileSystem.File.ReadAllText(FilePath)) ?? new UserStoreData();
                    _users = data.Users ?? new List<UserModel>();
                    _connections = data.Connections ?? new List<ConnectionModel>();

                    bool migrated = false;
                    foreach (ConnectionModel connection in _connections)
                    {
                        migrated |= _connectionPasswordProtector.MigrateLegacyPassword(connection);
                    }
                    if (migrated)
                    {
                        _logger.LogInformation("Migrated legacy connection passwords to data protection");
                        Write();
                    }
                }
                else
                {
                    // first time - create a default admin user
                    _users = new List<UserModel>
                    {
                        new UserModel
                        {
                            UserName = "admin",
                            Password = _passwordProvider.HashPassword("admin"),
                            Role = Roles.Admin
                        }
                    };
                    _connections = new List<ConnectionModel>();
                }
            }
        }

        public void SaveConnection(ConnectionModel connection)
        {
            lock (_syncRoot)
            {
                Replace(_connections, connection, x => x.Id == connection.Id);
                Write();
            }
        }

        public void SaveUser(UserModel user)
        {
            lock (_syncRoot)
            {
                Replace(_users, user, x => x.Id == user.Id);
                Write();
            }
        }

        public void DeleteConnection(Guid connectionId)
        {
            lock (_syncRoot)
            {
                _connections.RemoveAll(x => x.Id == connectionId);
                Write();
            }
        }

        public void DeleteUser(Guid userId)
        {
            lock (_syncRoot)
            {
                _users.RemoveAll(x => x.Id == userId);
                Write();
            }
        }

        private static void Replace<T>(List<T> items, T item, Predicate<T> match)
        {
            int index = items.FindIndex(match);
            if (index > -1)
            {
                items[index] = item;
            }
            else
            {
                items.Add(item);
            }
        }

        private void Write()
        {
            var data = new UserStoreData { Users = _users, Connections = _connections };
            string contents = JsonConvert.SerializeObject(data, Formatting.Indented);
            _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(FilePath)!);
            _fileSystem.File.WriteAllText(FilePath, contents);
        }

        /// <summary>
        /// Shape of the users.json file.
        /// </summary>
        private class UserStoreData
        {
            public List<UserModel>? Users { get; set; }

            public List<ConnectionModel>? Connections { get; set; }
        }
    }
}
