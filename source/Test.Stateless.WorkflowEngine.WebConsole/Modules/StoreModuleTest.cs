using AutoMapper;
using Encryption;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.Responses.Negotiation;
using Nancy.Testing;
using Nancy.ViewEngines.Razor;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Store;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class StoreModuleTest
    {
        private IUserStore _userStore;
        private IWorkflowInfoService _workflowInfoService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        [SetUp]
        public void UserModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _workflowInfoService = Substitute.For<IWorkflowInfoService>();
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();
        }

        #region Default Tests

        [Test]
        public void Default_NoConnectionFound_ReturnsInternalServerError()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);

            var connectionId = Guid.NewGuid();
            ConnectionModel connection = null;

            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            try
            {
                var response = browser.Get(Actions.Store.Default, (with) =>
                {
                    with.HttpRequest();
                    with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                    with.Query("id", connectionId.ToString());
                });
                Assert.Fail("Expected exception to be thrown with invalid connection id supplied");
            }
            catch (Exception ex)
            {
                // assert
                //Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
                //Assert.IsTrue(response.Body.AsString().Contains("No connection found"));
                Assert.IsInstanceOf<ArgumentException>(ex.InnerException.InnerException);
                Assert.IsTrue(ex.InnerException.InnerException.Message.Contains("No connection found"));
            }
            _userStore.Received(1).GetConnection(connectionId);
        }

        [Test]
        public void Default_ConnectionFound_SetsModelAndReturnsView()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            var response = browser.Get(Actions.Store.Default, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.Query("id", connectionId.ToString());
            });

            // assert
            string responseBody = response.Body.AsString();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(response.ContentType, "text/html");
            

            response.Body["title"]
                .ShouldExistOnce()
                .And.ShouldContain(connection.Host);
        }
        #endregion

        #region List Tests

        [Test]
        public void List_NoConnectionFound_ReturnsNotFoundResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();
            ConnectionModel connection = null;

            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            var response = browser.Post(Actions.Store.List, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            _userStore.Received(1).GetConnection(connectionId);
        }

        [Test]
        public void List_ConnectionFound_SetsModelAndReturnsView()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();
            int count = new Random().Next(5, 20);

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);

            List<UIWorkflow> workflows = new List<UIWorkflow>();
            for (int i = 0; i < count; i++)
            {
                UIWorkflow wf = new UIWorkflow();
                wf.Id = Guid.NewGuid();
                wf.WorkflowType = typeof(UIWorkflow).FullName;
                workflows.Add(wf);
            }
            _workflowInfoService.GetIncompleteWorkflows(connection, 50).Returns(workflows);

            // execute
            var response = browser.Post(Actions.Store.List, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("text/html", response.ContentType);

            // SUCKS - for some reason this view throws a reference exception trying to load 
            // Stateless.WorkflowEngine which is handled in the app with the config file.  Not sure 
            // how to do this programatically so leaving this test for now...
            // Adding the assembly to the test RazorConfiguration didn't help and broke other tests....

            //string responseBody = response.Body.AsString();
            //response.Body["table"].ShouldExistOnce();

            //response.Body["td"]
            //    .ShouldExistExactly(count);
        }
        #endregion

        #region Remove Tests

        [Test]
        public void Remove_NoConnectionFound_ReturnsNotFoundResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            ConnectionModel connection = null;

            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            var response = browser.Post(Actions.Store.Remove, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            _workflowStoreFactory.DidNotReceive().GetWorkflowStore(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Remove_SingleWorkflow_DeletesWorkflow()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);

            // execute
            var response = browser.Post(Actions.Store.Remove, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _workflowStoreFactory.Received(1).GetWorkflowStore(connection);
            workflowStore.Received(1).Delete(workflowId);
        }

        [Test]
        public void Remove_MultipleWorkflows_DeletesWorkflows()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId1 = Guid.NewGuid();
            var workflowId2 = Guid.NewGuid();
            var workflowId3 = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);

            // execute
            var response = browser.Post(Actions.Store.Remove, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId1.ToString());
                with.FormValue("WorkflowIds", workflowId2.ToString());
                with.FormValue("WorkflowIds", workflowId3.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _workflowStoreFactory.Received(1).GetWorkflowStore(connection);
            workflowStore.Received(1).Delete(workflowId1);
            workflowStore.Received(1).Delete(workflowId2);
            workflowStore.Received(1).Delete(workflowId3);
        }
        #endregion

        #region Suspend Tests

        [Test]
        public void Suspend_NoConnectionFound_ReturnsNotFoundResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            ConnectionModel connection = null;

            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            var response = browser.Post(Actions.Store.Suspend, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            _workflowStoreFactory.DidNotReceive().GetWorkflowStore(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Suspend_SingleWorkflow_SuspendsWorkflow()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);

            // execute
            var response = browser.Post(Actions.Store.Suspend, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _workflowStoreFactory.Received(1).GetWorkflowStore(connection);
            workflowStore.Received(1).SuspendWorkflow(workflowId);
        }

        [Test]
        public void Suspend_MultipleWorkflows_SuspendsWorkflows()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId1 = Guid.NewGuid();
            var workflowId2 = Guid.NewGuid();
            var workflowId3 = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);

            // execute
            var response = browser.Post(Actions.Store.Suspend, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId1.ToString());
                with.FormValue("WorkflowIds", workflowId2.ToString());
                with.FormValue("WorkflowIds", workflowId3.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _workflowStoreFactory.Received(1).GetWorkflowStore(connection);
            workflowStore.Received(1).SuspendWorkflow(workflowId1);
            workflowStore.Received(1).SuspendWorkflow(workflowId2);
            workflowStore.Received(1).SuspendWorkflow(workflowId3);
        }
        #endregion

        #region Unsuspend Tests

        [Test]
        public void Unsuspend_NoConnectionFound_ReturnsNotFoundResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            ConnectionModel connection = null;

            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            var response = browser.Post(Actions.Store.Unsuspend, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            _workflowStoreFactory.DidNotReceive().GetWorkflowStore(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Unsuspend_SingleWorkflow_UnuspendsWorkflow()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);

            // execute
            var response = browser.Post(Actions.Store.Unsuspend, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _workflowStoreFactory.Received(1).GetWorkflowStore(connection);
            workflowStore.Received(1).UnsuspendWorkflow(workflowId);
        }

        [Test]
        public void Unsuspend_MultipleWorkflows_UnsuspendsWorkflows()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId1 = Guid.NewGuid();
            var workflowId2 = Guid.NewGuid();
            var workflowId3 = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);

            // execute
            var response = browser.Post(Actions.Store.Unsuspend, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowIds", workflowId1.ToString());
                with.FormValue("WorkflowIds", workflowId2.ToString());
                with.FormValue("WorkflowIds", workflowId3.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _workflowStoreFactory.Received(1).GetWorkflowStore(connection);
            workflowStore.Received(1).UnsuspendWorkflow(workflowId1);
            workflowStore.Received(1).UnsuspendWorkflow(workflowId2);
            workflowStore.Received(1).UnsuspendWorkflow(workflowId3);
        }
        #endregion

        #region Workflow Methods

        [Test]
        public void Workflow_NoWorkflowFound_ReturnsNotFoundResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver"
            };

            _userStore.GetConnection(connectionId).Returns(connection);

            string json = null;
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);
            workflowStore.GetWorkflowAsJson(workflowId).Returns(json);

            // execute
            var response = browser.Post(Actions.Store.Workflow, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowId", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            workflowStore.Received(1).GetWorkflowAsJson(workflowId);
        }

        [Test]
        public void Workflow_WorkflowFound_SetsModelAndReturnsView()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var workflowId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            UIWorkflow uiWorkflow = new UIWorkflow();
            uiWorkflow.IsSuspended = DateTime.Now.Second > 30;

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId,
                Host = "myserver",
                WorkflowStoreType = WorkflowStoreType.MongoDb
            };
            _userStore.GetConnection(connectionId).Returns(connection);

            string json = JsonConvert.SerializeObject(uiWorkflow);
            
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(workflowStore);
            workflowStore.GetWorkflowAsJson(workflowId).Returns(json);

            _workflowInfoService.GetWorkflowInfoFromJson(json, WorkflowStoreType.MongoDb).Returns(uiWorkflow);
            
            // execute
            var response = browser.Post(Actions.Store.Workflow, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("WorkflowId", workflowId.ToString());
                with.FormValue("ConnectionId", connectionId.ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("application/json; charset=utf-8", response.ContentType);

            WorkflowViewModel wvm = JsonConvert.DeserializeObject<WorkflowViewModel>(response.Body.AsString());
            Assert.IsNotNull(wvm);
            Assert.AreEqual(json, wvm.WorkflowJson);
            Assert.AreEqual(uiWorkflow.IsSuspended, wvm.IsSuspended);
        }

        #endregion

        #region Private Methods

        private Browser CreateBrowser(UserIdentity currentUser)
        {
            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new StoreModule(_userStore, _workflowInfoService, _workflowStoreFactory))
                                .RootPathProvider(new TestRootPathProvider())
                                .RequestStartup((container, pipelines, context) => {
                                    context.CurrentUser = currentUser;
                                    context.ViewBag.Scripts = new List<string>();
                                    context.ViewBag.Claims = new List<string>();
                                    context.CurrentUser = currentUser;
                                    if (currentUser != null)
                                    {
                                        context.ViewBag.CurrentUserName = currentUser.UserName;
                                    }
                                })
                            );
            return browser;
        }

        private List<UserModel> ConfigureUsers(UserIdentity currentUser, string[] claims)
        {
            // set up the logged in user
            UserModel user = new UserModel()
            {
                Id = currentUser.Id,
                UserName = currentUser.UserName,
                Role = Roles.User,
                Claims = claims
            };
            List<UserModel> users = new List<UserModel>() { user };
            _userStore.Users.Returns(users);
            return users;
        }

        #endregion


    }
}
