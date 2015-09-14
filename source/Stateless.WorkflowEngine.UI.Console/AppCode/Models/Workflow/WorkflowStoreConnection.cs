using Encryption;
using Stateless.WorkflowEngine.UI.Console.AppCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Models.Workflow
{
    public enum WorkflowStoreType
    {
        None = 0,
        MongoDb = 1
    }

    public class WorkflowStoreConnection
    {
        public WorkflowStoreConnection()
        {
        }

        public WorkflowStoreConnection(WorkflowStoreType type, string host, int port, string databaseName, string userName, string password, string activeCollection, string completeCollection)
        {
            this.WorkflowStoreType = type;
            this.Host = host;
            this.Port = port;
            this.DatabaseName = databaseName;
            this.UserName = userName;
            this.Password = password;
            this.ActiveCollection = activeCollection;
            this.CompleteCollection = completeCollection;
        }

        public WorkflowStoreType WorkflowStoreType { get; set; }

        public string Image
        {
            get
            {
                return "/Stateless.WorkflowEngine.UI.Console;component/Resources/mongo_24x24.png";
            }
        }

        public string Host { get; set; }

        public int Port { get; set; }

        public string DatabaseName { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string ActiveCollection { get; set; }

        public string CompleteCollection { get; set; }

        public string DecryptPassword()
        {
            return String.IsNullOrWhiteSpace(this.Password) ? null : AESGCM.SimpleDecryptWithPassword(this.Password, Constants.PasswordKey);
        }

        public static string EncryptPassword(string plainTextPassword)
        {
            if (String.IsNullOrWhiteSpace(plainTextPassword))
            {
                return null;
            }
            return AESGCM.SimpleEncryptWithPassword(plainTextPassword, Constants.PasswordKey);
        }

        public override bool Equals(object obj)
        {
            WorkflowStoreConnection other = obj as WorkflowStoreConnection;
            if (other == null) return false;

            if (
                this.WorkflowStoreType == other.WorkflowStoreType &&
                this.Host == other.Host &&
                this.Port == other.Port &&
                this.DatabaseName == other.DatabaseName &&
                this.UserName == other.UserName &&
                this.Password == other.Password &&
                this.ActiveCollection == other.ActiveCollection &&
                this.CompleteCollection == other.CompleteCollection)
            {
                return true;
            }

            return base.Equals(obj);
        }
    }
}
