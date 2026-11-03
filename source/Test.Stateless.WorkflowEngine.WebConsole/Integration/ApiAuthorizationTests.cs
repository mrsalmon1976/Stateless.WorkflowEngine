using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;
using System.Net;
using System.Net.Http.Json;

namespace Test.Stateless.WorkflowEngine.WebConsole.Integration
{
    /// <summary>
    /// Runs the real application pipeline (cookie auth, policies, routing, SPA fallback) with the workflow
    /// store substituted.
    /// </summary>
    [TestFixture]
    public class ApiAuthorizationTests
    {
        private const string Password = "password1";

        private static readonly Guid ConnectionId = Guid.NewGuid();
        private static readonly Guid DashboardId = Guid.NewGuid();
        private static readonly Guid WorkflowId = Guid.NewGuid();
        private static readonly Guid LimitedUserId = Guid.NewGuid();
        private static readonly Guid TargetUserId = Guid.NewGuid();

        private string _rootPath = null!;
        private WebApplicationFactory<Program> _factory = null!;

        public record Endpoint(string Method, string Url, object? Body, string? Claim);

        private static IEnumerable<Endpoint> Endpoints()
        {
            var connection = new { workflowStoreType = "MongoDb", host = "localhost", port = 27017, database = "db", activeCollection = "Workflows", completedCollection = "CompletedWorkflows" };
            var user = new { userName = "newuser", password = "secret1", confirmPassword = "secret1", role = Roles.User };
            var ids = new { workflowIds = new[] { WorkflowId } };

            yield return new Endpoint("GET", "/api/auth/me", null, null);
            yield return new Endpoint("GET", "/api/connections", null, null);
            yield return new Endpoint("GET", $"/api/connections/{ConnectionId}", null, null);
            yield return new Endpoint("GET", $"/api/connections/{ConnectionId}/info", null, null);
            yield return new Endpoint("POST", "/api/connections", connection, Claims.ConnectionAdd);
            yield return new Endpoint("PUT", $"/api/connections/{ConnectionId}", connection, Claims.ConnectionEdit);
            yield return new Endpoint("POST", "/api/connections/test", connection, Claims.ConnectionAdd);
            yield return new Endpoint("POST", $"/api/connections/{ConnectionId}/copy", new { dashboardId = DashboardId }, Claims.ConnectionAdd);
            yield return new Endpoint("GET", "/api/dashboards", null, null);
            yield return new Endpoint("GET", $"/api/dashboards/{DashboardId}", null, null);
            yield return new Endpoint("POST", "/api/dashboards", new { name = "New dashboard" }, Claims.DashboardAdd);
            yield return new Endpoint("PUT", $"/api/dashboards/{DashboardId}", new { name = "Renamed" }, Claims.DashboardEdit);
            yield return new Endpoint("GET", $"/api/connections/{ConnectionId}/workflows", null, null);
            yield return new Endpoint("GET", $"/api/connections/{ConnectionId}/workflows/{WorkflowId}", null, null);
            yield return new Endpoint("GET", $"/api/connections/{ConnectionId}/definitions?qualifiedName=Test.Workflow", null, null);
            yield return new Endpoint("POST", $"/api/connections/{ConnectionId}/workflows/suspend", ids, Claims.SuspendWorkflow);
            yield return new Endpoint("POST", $"/api/connections/{ConnectionId}/workflows/unsuspend", ids, Claims.UnsuspendWorkflow);
            yield return new Endpoint("POST", $"/api/connections/{ConnectionId}/workflows/delete", ids, Claims.RemoveWorkflow);
            yield return new Endpoint("GET", "/api/users", null, Claims.UserList);
            yield return new Endpoint("POST", "/api/users", user, Claims.UserAdd);
            yield return new Endpoint("PUT", $"/api/users/{TargetUserId}", user, Claims.UserEdit);
            yield return new Endpoint("POST", "/api/users/me/password", new { password = Password, confirmPassword = Password }, null);
            yield return new Endpoint("GET", "/api/version/check", null, null);
            // destructive calls last so they don't affect the other cases
            yield return new Endpoint("DELETE", $"/api/users/{TargetUserId}", null, Claims.UserDelete);
            yield return new Endpoint("DELETE", $"/api/connections/{ConnectionId}", null, Claims.ConnectionDelete);
            yield return new Endpoint("DELETE", $"/api/dashboards/{DashboardId}", null, Claims.DashboardDelete);
        }

        private static IEnumerable<Endpoint> ProtectedEndpoints() => Endpoints().Where(x => x.Claim != null);

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), "WebConsoleTests", Guid.NewGuid().ToString());
            string dataPath = Path.Combine(_rootPath, "Data");
            string webRootPath = Path.Combine(_rootPath, "wwwroot");
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(webRootPath);
            // note: when the client has been built, the real wwwroot/index.html is served via static web assets instead
            File.WriteAllText(Path.Combine(webRootPath, "index.html"), "<html><div id=\"app\"></div></html>");
            WriteUserStore(Path.Combine(dataPath, "users.json"));

            var workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetIncompleteWorkflowsAsJsonAsync(Arg.Any<int>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));
            workflowStore.GetWorkflowAsJsonAsync(WorkflowId).Returns(Task.FromResult(JsonConvert.SerializeObject(new { Workflow = new { Id = WorkflowId } })));
            workflowStore.GetDefinitionByQualifiedNameAsync("Test.Workflow").Returns(Task.FromResult(new WorkflowDefinition { QualifiedName = "Test.Workflow" }));
            var workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();
            workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(workflowStore);
            var versionCheckService = Substitute.For<IVersionCheckService>();
            versionCheckService.CheckIfNewVersionAvailable().Returns(new VersionCheckResult());

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("DataPath", dataPath);
                builder.UseSetting(WebHostDefaults.WebRootKey, webRootPath);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IWorkflowStoreFactory>();
                    services.AddSingleton(workflowStoreFactory);
                    services.RemoveAll<IVersionCheckService>();
                    services.AddSingleton(versionCheckService);
                });
            });
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
            try { Directory.Delete(_rootPath, true); } catch (IOException) { }
        }

        [TestCaseSource(nameof(Endpoints))]
        public async Task Endpoint_Returns401_WhenAnonymous(Endpoint endpoint)
        {
            using var client = CreateClient();

            var response = await SendAsync(client, endpoint);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [TestCaseSource(nameof(ProtectedEndpoints))]
        public async Task Endpoint_Returns403_WhenUserLacksClaim(Endpoint endpoint)
        {
            using var client = await LoginAsync("limited");

            var response = await SendAsync(client, endpoint);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [TestCaseSource(nameof(ProtectedEndpoints))]
        public async Task Endpoint_Succeeds_WhenUserHasOnlyRequiredClaim(Endpoint endpoint)
        {
            using var admin = await LoginAsync("admin");
            await admin.PutAsJsonAsync($"/api/users/{LimitedUserId}", new { userName = "limited", role = Roles.User, claims = new[] { endpoint.Claim } });
            using var client = await LoginAsync("limited");

            var response = await SendAsync(client, endpoint);

            Assert.That((int)response.StatusCode, Is.InRange(200, 299), await response.Content.ReadAsStringAsync());
        }

        [TestCaseSource(nameof(Endpoints))]
        public async Task Endpoint_Succeeds_WhenAdmin(Endpoint endpoint)
        {
            using var client = await LoginAsync("admin");

            var response = await SendAsync(client, endpoint);

            Assert.That((int)response.StatusCode, Is.InRange(200, 299), await response.Content.ReadAsStringAsync());
        }

        [Test]
        public async Task Login_Returns400_WhenPasswordIncorrect()
        {
            using var client = CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "admin", password = "wrong" });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task DeletedUser_IsSignedOut()
        {
            using var limited = await LoginAsync("limited");
            using var admin = await LoginAsync("admin");

            await admin.DeleteAsync($"/api/users/{LimitedUserId}");
            var response = await limited.GetAsync("/api/auth/me");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task UnknownApiRoute_Returns404()
        {
            using var client = CreateClient();

            var response = await client.GetAsync("/api/does-not-exist");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [TestCase("/")]
        [TestCase("/dashboard")]
        [TestCase("/connections/abc/workflows")]
        public async Task ClientRoutes_ServeSpa(string url)
        {
            using var client = CreateClient();

            var response = await client.GetAsync(url);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("id=\"app\""));
        }

        private HttpClient CreateClient()
        {
            return _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        }

        private async Task<HttpClient> LoginAsync(string userName)
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password = Password });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Login failed for {userName}");
            return client;
        }

        private static Task<HttpResponseMessage> SendAsync(HttpClient client, Endpoint endpoint)
        {
            var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Url);
            if (endpoint.Body != null)
            {
                request.Content = JsonContent.Create(endpoint.Body);
            }
            return client.SendAsync(request);
        }

        private static void WriteUserStore(string filePath)
        {
            var passwordProvider = new PasswordProvider();
            string hash = passwordProvider.HashPassword(Password);
            var data = new
            {
                Users = new[]
                {
                    new UserModel { UserName = "admin", Password = hash, Role = Roles.Admin },
                    new UserModel { Id = LimitedUserId, UserName = "limited", Password = hash, Role = Roles.User, Claims = Array.Empty<string>() },
                    new UserModel { Id = TargetUserId, UserName = "target", Password = hash, Role = Roles.User }
                },
                Dashboards = new[]
                {
                    new DashboardModel { Id = DashboardId, Name = "Other" }
                },
                Connections = new[]
                {
                    new ConnectionModel { Id = ConnectionId, Host = "localhost", Port = 27017, Database = "db", ActiveCollection = "Workflows", CompletedCollection = "CompletedWorkflows" }
                }
            };
            File.WriteAllText(filePath, JsonConvert.SerializeObject(data));
        }
    }
}
