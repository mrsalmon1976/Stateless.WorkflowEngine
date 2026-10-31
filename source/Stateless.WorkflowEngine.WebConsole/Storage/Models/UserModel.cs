namespace Stateless.WorkflowEngine.WebConsole.Storage.Models
{
    public class UserModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string? UserName { get; set; }

        /// <summary>
        /// Gets/sets the BCrypt hash of the user's password.
        /// </summary>
        public string? Password { get; set; }

        public string? Role { get; set; }

        public string[]? Claims { get; set; }
    }
}
