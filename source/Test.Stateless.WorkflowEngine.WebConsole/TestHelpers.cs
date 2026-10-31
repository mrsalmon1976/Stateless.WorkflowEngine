using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Security;

namespace Test.Stateless.WorkflowEngine.WebConsole
{
    internal static class TestHelpers
    {
        public static UserModel CreateUser(string userName = "user", string role = Roles.User, params string[] claims)
        {
            return new UserModel
            {
                UserName = userName,
                Password = "hash",
                Role = role,
                Claims = claims
            };
        }

        public static ConnectionModel CreateConnection(string host = "localhost", string database = "db")
        {
            return new ConnectionModel
            {
                WorkflowStoreType = WorkflowStoreType.MongoDb,
                Host = host,
                Port = 27017,
                Database = database,
                ActiveCollection = "Workflows",
                CompletedCollection = "CompletedWorkflows"
            };
        }

        /// <summary>
        /// Sets the controller up with a signed in user.
        /// </summary>
        public static T WithUser<T>(this T controller, UserModel user) where T : ControllerBase
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = UserPrincipalFactory.Create(user) }
            };
            return controller;
        }

        public static IReadOnlyList<string> GetErrors(IActionResult result)
        {
            var badRequest = result as BadRequestObjectResult;
            Assert.That(badRequest, Is.Not.Null, "Expected a BadRequest result");
            var errors = badRequest!.Value!.GetType().GetProperty("errors")!.GetValue(badRequest.Value) as IEnumerable<string>;
            return errors!.ToList();
        }
    }
}
