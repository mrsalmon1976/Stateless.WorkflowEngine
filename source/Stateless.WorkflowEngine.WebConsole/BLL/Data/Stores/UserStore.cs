using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemWrapper.IO;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores
{
    public interface IUserStore
    {
        /// <summary>
        /// Loads the users for the app from the file on disk.
        /// </summary>
        void Load();

        /// <summary>
        /// Gets the users of the application.
        /// </summary>
        List<UserModel> Users { get; }

        /// <summary>
        /// Saves the user store to disk.
        /// </summary>
        void Save();
        
    }

    public class UserStore : IUserStore
    {
        private string _filePath;
        private IFileWrap _fileWrap;
        private IDirectoryWrap _dirWrap;
        private IPasswordProvider _passwordProvider;

        public UserStore(string filePath, IFileWrap fileWrap, IDirectoryWrap dirWrap, IPasswordProvider passwordProvider)
        {
            _filePath = filePath;
            _fileWrap = fileWrap;
            _dirWrap = dirWrap;
            _passwordProvider = passwordProvider;
            this.Users = new List<UserModel>();
        }

        public string RootPath { get; set; }

        public string FileName { get; set; }

        /// <summary>
        /// Gets the users of the application.
        /// </summary>
        public List<UserModel> Users { get; private set; }

        /// <summary>
        /// Loads the users for the app from the file on disk.
        /// </summary>
        public void Load()
        {
            if (_fileWrap.Exists(_filePath))
            {
                string sUsers = _fileWrap.ReadAllText(_filePath);
                List<UserModel> users = JsonConvert.DeserializeObject<List<UserModel>>(sUsers);
                this.Users.AddRange(users);
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
            string contents = JsonConvert.SerializeObject(this.Users);
            _dirWrap.CreateDirectory(Path.GetDirectoryName(_filePath));
            _fileWrap.WriteAllText(_filePath, contents);
        }
    }
}
