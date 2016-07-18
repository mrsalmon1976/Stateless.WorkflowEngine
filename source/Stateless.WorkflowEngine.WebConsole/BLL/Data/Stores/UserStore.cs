using Newtonsoft.Json;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using System;
using System.Collections.Generic;
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
        
    }

    public class UserStore : IUserStore
    {
        private string _filePath;
        private IFileWrap _fileWrap;
        private IPasswordProvider _passwordProvider;

        public UserStore(string filePath, IFileWrap fileWrap, IPasswordProvider passwordProvider)
        {
            _filePath = filePath;
            _fileWrap = fileWrap;
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
                Users.Add(user);
            }
        }
    }
}
