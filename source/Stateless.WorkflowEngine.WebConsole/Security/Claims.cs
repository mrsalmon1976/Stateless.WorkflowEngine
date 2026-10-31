namespace Stateless.WorkflowEngine.WebConsole.Security
{
    public static class Claims
    {
        public const string ConnectionAdd = "ConnectionAdd";

        public const string ConnectionDelete = "ConnectionDelete";

        public const string ConnectionEdit = "ConnectionEdit";

        public const string RemoveWorkflow = "RemoveWorkflow";

        public const string SuspendWorkflow = "SuspendWorkflow";

        public const string UnsuspendWorkflow = "UnsuspendWorkflow";

        public const string UserAdd = "UserAdd";

        public const string UserDelete = "UserDelete";

        public const string UserEdit = "UserEdit";

        public const string UserList = "UserList";

        /// <summary>
        /// The claim type used on the cookie principal for console permissions.
        /// </summary>
        public const string PermissionClaimType = "permission";

        public static IReadOnlyList<string> AllClaims { get; } = new[]
        {
            ConnectionAdd,
            ConnectionDelete,
            ConnectionEdit,
            RemoveWorkflow,
            SuspendWorkflow,
            UnsuspendWorkflow,
            UserAdd,
            UserDelete,
            UserEdit,
            UserList
        };
    }
}
