namespace Stateless.WorkflowEngine.WebConsole.Security
{
    public static class Roles
    {
        public const string Admin = "Admin";

        public const string User = "User";

        public static IReadOnlyList<string> AllRoles { get; } = new[] { Admin, User };
    }
}
