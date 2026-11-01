using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Security;
using System.IO.Abstractions;

namespace Stateless.WorkflowEngine.WebConsole.Storage.Stores
{
    /// <summary>
    /// File-based store for the users, dashboards and connections of the console.  The file format is shared
    /// with previous versions of the console so existing installs can be upgraded in place.
    /// </summary>
    public interface IUserStore
    {
        string FilePath { get; }

        IReadOnlyList<UserModel> Users { get; }

        IReadOnlyList<ConnectionModel> Connections { get; }

        IReadOnlyList<DashboardModel> Dashboards { get; }

        /// <summary>
        /// Gets the default dashboard - there is always one once the store has been loaded.
        /// </summary>
        DashboardModel DefaultDashboard { get; }

        ConnectionModel? GetConnection(Guid connectionId);

        DashboardModel? GetDashboard(Guid dashboardId);

        UserModel? GetUser(Guid userId);

        UserModel? GetUser(string userName);

        /// <summary>
        /// Loads the users, dashboards and connections from disk, creating a default admin user on first run,
        /// creating the default dashboard and migrating any legacy connection passwords.
        /// </summary>
        void Load();

        /// <summary>
        /// Adds or replaces (by Id) a connection and saves the store.
        /// </summary>
        void SaveConnection(ConnectionModel connection);

        /// <summary>
        /// Adds or replaces (by Id) a dashboard and saves the store.
        /// </summary>
        void SaveDashboard(DashboardModel dashboard);

        /// <summary>
        /// Adds or replaces (by Id) a user and saves the store.
        /// </summary>
        void SaveUser(UserModel user);

        void DeleteConnection(Guid connectionId);

        /// <summary>
        /// Deletes a dashboard along with all the connections on it.
        /// </summary>
        void DeleteDashboard(Guid dashboardId);

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
        private List<DashboardModel> _dashboards = new List<DashboardModel>();

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

        public IReadOnlyList<DashboardModel> Dashboards
        {
            get { lock (_syncRoot) { return _dashboards.ToList(); } }
        }

        public DashboardModel DefaultDashboard
        {
            get { lock (_syncRoot) { return _dashboards.Single(x => x.IsDefault); } }
        }

        public ConnectionModel? GetConnection(Guid connectionId)
        {
            return Connections.SingleOrDefault(x => x.Id == connectionId);
        }

        public DashboardModel? GetDashboard(Guid dashboardId)
        {
            return Dashboards.SingleOrDefault(x => x.Id == dashboardId);
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
                    _dashboards = data.Dashboards ?? new List<DashboardModel>();

                    bool migrated = EnsureDefaultDashboard();
                    foreach (ConnectionModel connection in _connections)
                    {
                        migrated |= _connectionPasswordProtector.MigrateLegacyPassword(connection);
                    }
                    if (migrated)
                    {
                        _logger.LogInformation("Migrated connections to dashboards and data protection");
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
                    _dashboards = new List<DashboardModel>();
                    EnsureDefaultDashboard();
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

        public void SaveDashboard(DashboardModel dashboard)
        {
            lock (_syncRoot)
            {
                Replace(_dashboards, dashboard, x => x.Id == dashboard.Id);
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

        public void DeleteDashboard(Guid dashboardId)
        {
            lock (_syncRoot)
            {
                _dashboards.RemoveAll(x => x.Id == dashboardId);
                _connections.RemoveAll(x => x.DashboardId == dashboardId);
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

        /// <summary>
        /// Makes sure there is exactly one default dashboard and that every connection is on a dashboard that
        /// exists - connections saved by previous versions have no dashboard so go on the default dashboard.
        /// Returns true if anything changed.
        /// </summary>
        private bool EnsureDefaultDashboard()
        {
            bool changed = false;
            DashboardModel? defaultDashboard = _dashboards.FirstOrDefault(x => x.IsDefault);
            if (defaultDashboard == null)
            {
                defaultDashboard = new DashboardModel { Name = DashboardModel.DefaultName, IsDefault = true };
                _dashboards.Insert(0, defaultDashboard);
                changed = true;
            }
            foreach (DashboardModel dashboard in _dashboards.Where(x => x.IsDefault && x != defaultDashboard))
            {
                dashboard.IsDefault = false;
                changed = true;
            }

            var dashboardIds = _dashboards.Select(x => x.Id).ToHashSet();
            foreach (ConnectionModel connection in _connections.Where(x => !x.DashboardId.HasValue || !dashboardIds.Contains(x.DashboardId.Value)))
            {
                connection.DashboardId = defaultDashboard.Id;
                changed = true;
            }
            return changed;
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
            var data = new UserStoreData { Users = _users, Dashboards = _dashboards, Connections = _connections };
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

            public List<DashboardModel>? Dashboards { get; set; }

            public List<ConnectionModel>? Connections { get; set; }
        }
    }
}
