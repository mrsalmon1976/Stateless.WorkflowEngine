using Microsoft.AspNetCore.DataProtection;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;

namespace Stateless.WorkflowEngine.WebConsole.Security
{
    /// <summary>
    /// Encrypts and decrypts connection passwords using ASP.NET Core data protection, with support for
    /// migrating passwords encrypted by older versions of the console.
    /// </summary>
    public interface IConnectionPasswordProtector
    {
        string Protect(string password);

        string? Unprotect(ConnectionModel connection);

        /// <summary>
        /// Re-encrypts a password stored by an older version of the console using data protection.  Returns
        /// true if the connection was changed.
        /// </summary>
        bool MigrateLegacyPassword(ConnectionModel connection);
    }

    public class ConnectionPasswordProtector : IConnectionPasswordProtector
    {
        public const string Purpose = "ConnectionPassword";

        private readonly IDataProtector _dataProtector;
        private readonly ILegacyEncryptionProvider _legacyEncryptionProvider;

        public ConnectionPasswordProtector(IDataProtectionProvider dataProtectionProvider, ILegacyEncryptionProvider legacyEncryptionProvider)
        {
            _dataProtector = dataProtectionProvider.CreateProtector(Purpose);
            _legacyEncryptionProvider = legacyEncryptionProvider;
        }

        public string Protect(string password)
        {
            return _dataProtector.Protect(password);
        }

        public string? Unprotect(ConnectionModel connection)
        {
            if (String.IsNullOrEmpty(connection.Password)) return null;

            if (!String.IsNullOrEmpty(connection.Key))
            {
                return _legacyEncryptionProvider.SimpleDecrypt(connection.Password, Convert.FromBase64String(connection.Key));
            }
            return _dataProtector.Unprotect(connection.Password);
        }

        public bool MigrateLegacyPassword(ConnectionModel connection)
        {
            if (String.IsNullOrEmpty(connection.Key)) return false;

            string? password = String.IsNullOrEmpty(connection.Password) ? null : Unprotect(connection);
            connection.Password = (password == null ? null : Protect(password));
            connection.Key = null;
            return true;
        }
    }
}
