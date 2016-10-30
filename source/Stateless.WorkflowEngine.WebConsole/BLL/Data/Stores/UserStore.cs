using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using SystemWrapper.IO;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores
{
    public interface IUserStore
    {
        /// <summary>
        /// Gets/sets the file path of the file store.
        /// </summary>
        string FilePath { get; set; }

        /// <summary>
        /// Gets the users of the application.
        /// </summary>
        List<UserModel> Users { get; }

        /// <summary>
        /// Gets/sets the configured connections.
        /// </summary>
        List<ConnectionModel> Connections { get; set; }

        /// <summary>
        /// Gets a user by user name.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        UserModel GetUser(string userName);

        /// <summary>
        /// Gets a connection by it's unique id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        ConnectionModel GetConnection(Guid connectionId);

        /// <summary>
        /// Loads the users for the app from the file on disk.
        /// </summary>
        void Load();

        /// <summary>
        /// Saves the user store to disk.
        /// </summary>
        void Save();
        
    }

    public class UserStore : IUserStore
    {
        private IFileWrap _fileWrap;
        private IDirectoryWrap _dirWrap;
        private IPasswordProvider _passwordProvider;

        public UserStore(string filePath, IFileWrap fileWrap, IDirectoryWrap dirWrap, IPasswordProvider passwordProvider)
        {
            this.FilePath = filePath;
            _fileWrap = fileWrap;
            _dirWrap = dirWrap;
            _passwordProvider = passwordProvider;
            this.Users = new List<UserModel>();
            this.Connections = new List<ConnectionModel>();
        }

        [IgnoreDataMember]
        public string FilePath { get; set; }

        /// <summary>
        /// Gets the users of the application.
        /// </summary>
        public List<UserModel> Users { get; private set; }

        /// <summary>
        /// Gets/sets the configured connections.
        /// </summary>
        public List<ConnectionModel> Connections { get; set; }

        /// <summary>
        /// Gets a user by user name.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public UserModel GetUser(string userName)
        {
            return this.Users.Where(x => x.UserName == userName).FirstOrDefault();
        }

        /// <summary>
        /// Gets a connection by it's unique id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public ConnectionModel GetConnection(Guid connectionId)
        {
            return this.Connections.Where(x => x.Id == connectionId).SingleOrDefault();
        }

        /// <summary>
        /// Loads the users for the app from the file on disk.
        /// </summary>
        public void Load()
        {
            if (_fileWrap.Exists(this.FilePath))
            {
                string text = _fileWrap.ReadAllText(this.FilePath);
                UserStore store = JsonConvert.DeserializeObject<UserStore>(text);
                this.Users.AddRange(store.Users);
                this.Connections.AddRange(store.Connections);
            }
            else
            {
                // first time - create a default admin user
                UserModel user = new UserModel();
                user.Id = Guid.NewGuid();
                user.UserName = "admin";
                user.Password = _passwordProvider.HashPassword("admin", _passwordProvider.GenerateSalt());
                user.Role = Roles.Admin;
                Users.Add(user);
            }
        }

        /// <summary>
        /// Saves the user store to disk.
        /// </summary>
        public void Save()
        {
            string contents = JsonConvert.SerializeObject(this, Formatting.Indented);
            _dirWrap.CreateDirectory(Path.GetDirectoryName(this.FilePath));
            _fileWrap.WriteAllText(this.FilePath, contents);
        }
    }
}
