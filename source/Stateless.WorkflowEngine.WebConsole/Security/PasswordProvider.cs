namespace Stateless.WorkflowEngine.WebConsole.Security
{
    public interface IPasswordProvider
    {
        bool CheckPassword(string password, string? hash);

        string HashPassword(string password);
    }

    public class PasswordProvider : IPasswordProvider
    {
        public bool CheckPassword(string password, string? hash)
        {
            if (String.IsNullOrEmpty(hash)) return false;
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt());
        }
    }
}
