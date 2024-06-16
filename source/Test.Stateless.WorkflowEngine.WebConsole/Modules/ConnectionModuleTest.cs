using AutoMapper;
using Encryption;
using Microsoft.Extensions.Caching.Memory;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.Responses.Negotiation;
using Nancy.Testing;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.Caching;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemWrapper.IO;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class ConnectionModuleTest
    {
        private IMapper _mapper;
        private ICacheProvider _cacheProvider;
        private IUserStore _userStore;
        private IConnectionValidator _connectionValidator;
        private IEncryptionProvider _encryptionProvider;
        private IWorkflowInfoService _workflowStoreService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        [SetUp]
        public void ConnectionModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _cacheProvider = Substitute.For<ICacheProvider>();
            _encryptionProvider = Substitute.For<IEncryptionProvider>();
            _connectionValidator = Substitute.For<IConnectionValidator>();
            _workflowStoreService = Substitute.For<IWorkflowInfoService>();
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();

            var config = new MapperConfiguration(cfg => {
                cfg.CreateMap<ConnectionViewModel, ConnectionModel>();
                cfg.CreateMap<ConnectionModel, ConnectionViewModel>();
            });
            _mapper = config.CreateMapper();

        }

        #region Delete Tests

        [Test]
        public void Delete_AuthTest()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId
            };
            _userStore.Connections.Returns(new List<ConnectionModel>() { connection });
            _userStore.GetConnection(connectionId).Returns(connection);

            foreach (string claim in Claims.AllClaims)
            {

                currentUser.Claims = new string[] { claim };

                // execute
                var response = browser.Post(Actions.Connection.Delete, (with) =>
                {
                    with.HttpRequest();
                    with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                    with.FormValue("id", connectionId.ToString());
                });

                // assert
                if (claim == Claims.ConnectionDelete)
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                }
                else
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                }
            }

        }

        [Test]
        public void Delete_NoConnectionFound_ReturnsNotFoundResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionDelete };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();

            _userStore.Connections.Returns(new List<ConnectionModel>());

            // execute
            var response = browser.Post(Actions.Connection.Delete, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            _userStore.DidNotReceive().Save();
        }

        [Test]
        public void Delete_ConnectionFound_RemovesConnection()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionDelete };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId
            };
            List<ConnectionModel> connections = new List<ConnectionModel>();
            connections.Add(connection);

            _userStore.Connections.Returns(connections);
            _userStore.GetConnection(connectionId).Returns(connection);

            // execute
            var response = browser.Post(Actions.Connection.Delete, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            Assert.That(_userStore.Connections.Count, Is.EqualTo(0));
            Assert.That(connections.Count, Is.EqualTo(0));
            _userStore.Received(1).Save();
        }
        #endregion

        #region Info Tests

        [Test]
        public void Info_ResponseCached_ReturnsFromCache()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionDelete };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid().ToString();
            string cacheKey = CacheKeys.ConnectionInfo(connectionId);
            ConnectionInfoViewModel cachedResult = new ConnectionInfoViewModel();
            cachedResult.ActiveCount = new Random().Next(100, 1000);
            _cacheProvider.Get<ConnectionInfoViewModel>(cacheKey).Returns(cachedResult);

            // execute
            var response = browser.Post(Actions.Connection.Info, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            _workflowStoreService.DidNotReceive().GetWorkflowStoreInfo(Arg.Any<ConnectionModel>());
            _cacheProvider.Received(1).Get<ConnectionInfoViewModel>(cacheKey);

            ConnectionInfoViewModel result = JsonConvert.DeserializeObject<ConnectionInfoViewModel>(response.Body.AsString());
            Assert.That(result.ActiveCount, Is.EqualTo(cachedResult.ActiveCount));
        }

        [Test]
        public void Info_NoConnectionFound_ReturnsNotFoundResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionDelete };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();

            _userStore.Connections.Returns(new List<ConnectionModel>());

            // execute
            var response = browser.Post(Actions.Connection.Info, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            _workflowStoreService.DidNotReceive().GetWorkflowStoreInfo(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Info_ConnectionFound_ReturnsConnectionInfo()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionDelete };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();

            ConnectionModel conn = new ConnectionModel();
            conn.Id = connectionId;
            _userStore.GetConnection(connectionId).Returns(conn);

            Random r = new Random();
            ConnectionInfoViewModel infoViewModel = new ConnectionInfoViewModel();
            infoViewModel.ActiveCount = r.Next(1, 10);
            infoViewModel.SuspendedCount = r.Next(11, 20);
            infoViewModel.CompleteCount = r.Next(100, 1000);
            _workflowStoreService.GetWorkflowStoreInfo(conn).Returns(infoViewModel);

            // execute
            var response = browser.Post(Actions.Connection.Info, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            _workflowStoreService.Received(1).GetWorkflowStoreInfo(conn);

            ConnectionInfoViewModel result = JsonConvert.DeserializeObject<ConnectionInfoViewModel>(response.Body.AsString());
            Assert.That(result.ActiveCount, Is.EqualTo(infoViewModel.ActiveCount));
            Assert.That(result.SuspendedCount, Is.EqualTo(infoViewModel.SuspendedCount));
            Assert.That(result.CompleteCount, Is.EqualTo(infoViewModel.CompleteCount));
        }

        [Test]
        public void Info_ConnectionFound_IsCached()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionDelete };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();
            string cacheKey = CacheKeys.ConnectionInfo(connectionId.ToString());

            ConnectionModel conn = new ConnectionModel();
            conn.Id = connectionId;
            _userStore.GetConnection(connectionId).Returns(conn);

            ConnectionInfoViewModel infoViewModel = new ConnectionInfoViewModel();
            _workflowStoreService.GetWorkflowStoreInfo(conn).Returns(infoViewModel);

            // execute
            var response = browser.Post(Actions.Connection.Info, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("id", connectionId.ToString());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            _cacheProvider.Received(1).Set<ConnectionInfoViewModel>(cacheKey, Arg.Any<ConnectionInfoViewModel>(), TimeSpan.FromSeconds(5));

        }


        #endregion

        #region List Tests

        [Test]
        public void List_OnExecute_OrdersConnectionsByHostThenDatabase()
        {
            List<ConnectionModel> connections = new List<ConnectionModel>();
            connections.Add(new ConnectionModel() { Host = "Z", Database = "A" });
            connections.Add(new ConnectionModel() { Host = "Y", Database = "z" });
            connections.Add(new ConnectionModel() { Host = "y", Database = "B" });
            connections.Add(new ConnectionModel() { Host = "Z", Database = "B" });
            connections.Add(new ConnectionModel() { Host = "a", Database = "A" });
            connections.Add(new ConnectionModel() { Host = "A", Database = "b" });
            _userStore.Connections.Returns(connections);

            // execute
            ConnectionModule module = new ConnectionModule(_mapper, _cacheProvider, _userStore, _connectionValidator, null, _workflowStoreService, _workflowStoreFactory);
            module.Context = new NancyContext();
            var result = module.List();

            // assert
            ConnectionListViewModel model = result.NegotiationContext.DefaultModel as ConnectionListViewModel;
            Assert.That(model, Is.Not.Null);
            Assert.That("a", Is.EqualTo(model.Connections[0].Host));
            Assert.That("A", Is.EqualTo(model.Connections[0].Database));
            Assert.That("A", Is.EqualTo(model.Connections[1].Host));
            Assert.That("b", Is.EqualTo(model.Connections[1].Database));
            Assert.That("y", Is.EqualTo(model.Connections[2].Host));
            Assert.That("B", Is.EqualTo(model.Connections[2].Database));
            Assert.That("Y", Is.EqualTo(model.Connections[3].Host));
            Assert.That("z", Is.EqualTo(model.Connections[3].Database));
            Assert.That("Z", Is.EqualTo(model.Connections[4].Host));
            Assert.That("A", Is.EqualTo(model.Connections[4].Database));
            Assert.That("Z", Is.EqualTo(model.Connections[5].Host));
            Assert.That("B", Is.EqualTo(model.Connections[5].Database));
        }

        [Test]
        public void List_UserHasConnectionDeleteClaim_CurrentUserCanDeleteConnectionOnModelIsTrue()
        {
            // setup
            List<ConnectionModel> connections = new List<ConnectionModel>();
            _userStore.Connections.Returns(connections);

            ConnectionModule module = new ConnectionModule(_mapper, _cacheProvider, _userStore, _connectionValidator, null, _workflowStoreService, _workflowStoreFactory);
            module.Context = new NancyContext();
            module.Context.CurrentUser = new UserIdentity()
            {
                Claims = new string[] { Claims.ConnectionDelete }
            };

            // execute
            var result = module.List();

            // assert
            ConnectionListViewModel model = result.NegotiationContext.DefaultModel as ConnectionListViewModel;
            Assert.That(model.CurrentUserCanDeleteConnection, Is.True);
        }

        [Test]
        public void List_UserHasConnectionDeleteClaim_CurrentUserCannotDeleteConnectionOnModelIsFalse()
        {
            // setup
            List<ConnectionModel> connections = new List<ConnectionModel>();
            _userStore.Connections.Returns(connections);

            ConnectionModule module = new ConnectionModule(_mapper, _cacheProvider, _userStore, _connectionValidator, _encryptionProvider, _workflowStoreService, _workflowStoreFactory);
            module.Context = new NancyContext();
            module.Context.CurrentUser = new UserIdentity()
            {
                Claims = new string[] { }
            };

            // execute
            var result = module.List();

            // assert
            ConnectionListViewModel model = result.NegotiationContext.DefaultModel as ConnectionListViewModel;
            Assert.That(model.CurrentUserCanDeleteConnection, Is.False);
        }

        #endregion

        #region Save Tests

        [Test]
        public void Save_AuthTest()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            var connectionId = Guid.NewGuid();

            ConnectionModel connection = new ConnectionModel()
            {
                Id = connectionId
            };
            _userStore.Connections.Returns(new List<ConnectionModel>() { connection });
            _userStore.GetConnection(connectionId).Returns(connection);

            foreach (string claim in Claims.AllClaims)
            {
                _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult());

                currentUser.Claims = new string[] { claim };

                // execute
                var response = browser.Post(Actions.Connection.Save, (with) =>
                {
                    with.HttpRequest();
                    with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                    with.FormValue("id", connectionId.ToString());
                });

                // assert
                if (claim == Claims.ConnectionAdd)
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                }
                else
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                }
            }

        }

        [Test]
        public void Save_InvalidModel_ReturnsError()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionAdd };
            var browser = CreateBrowser(currentUser);

            _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult("error"));

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            _encryptionProvider.DidNotReceive().SimpleEncrypt(Arg.Any<string>(), Arg.Any<byte[]>(), null);
            _userStore.DidNotReceive().Save();
        }

        [Test]
        public void Save_NoPassword_DoesNotEncrypt()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionAdd };
            var browser = CreateBrowser(currentUser);

            _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult());
            _userStore.Connections.Returns(new List<ConnectionModel>());

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", "");
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Count, Is.EqualTo(0));
            _encryptionProvider.DidNotReceive().SimpleEncrypt(Arg.Any<string>(), Arg.Any<byte[]>(), null);
            _userStore.Received(1).Save();
        }

        [Test]
        public void Save_WithPassword_DoesEncryptAndSaves()
        {
            // setup
            byte[] key = new byte[20];
            new Random().NextBytes(key);
            string password = "testPassword";
            string encryptedPassword = Guid.NewGuid().ToString();

            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionAdd };
            var browser = CreateBrowser(currentUser);

            _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult());
            _encryptionProvider.NewKey().Returns(key);
            _encryptionProvider.SimpleEncrypt(password, key, null).Returns(encryptedPassword);

            List<ConnectionModel> connections = new List<ConnectionModel>();
            _userStore.Connections.Returns(connections);

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", password);
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Count, Is.EqualTo(0));
            _encryptionProvider.Received(1).SimpleEncrypt(password, key, null);

            Assert.That(_userStore.Connections.Count, Is.EqualTo(1));
            Assert.That(_userStore.Connections[0].Password, Is.EqualTo(encryptedPassword));
            _userStore.Received(1).Save();
        }

        [Test]
        public void Save_ValidModel_ConnectionIdIsNotEmpty()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.ConnectionAdd };
            var browser = CreateBrowser(currentUser);

            _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult());

            List<ConnectionModel> connections = new List<ConnectionModel>();
            _userStore.Connections.Returns(connections);

            // execute
            var response = browser.Post(Actions.Connection.Save, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", "test");
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Count, Is.EqualTo(0));

            Assert.That(_userStore.Connections.Count, Is.EqualTo(1));
            Assert.That(_userStore.Connections[0].Id, Is.Not.EqualTo(Guid.Empty));
            _userStore.Received(1).Save();
        }

        #endregion

        #region Test Tests

        [Test]
        public void Test_InvalidModel_ReturnsError()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult("error"));

            // execute
            var response = browser.Post(Actions.Connection.Test, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            _workflowStoreFactory.DidNotReceive().GetWorkflowStore(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Test_ConnectionFails_ReturnsError()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult());

            // set up the workflow store to throw an exception
            IWorkflowStore store = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(store);
            store.When(x => x.GetIncompleteCount()).Do(x => { throw new Exception("connection error"); });

            // execute
            var response = browser.Post(Actions.Connection.Test, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            _workflowStoreFactory.Received(1).GetWorkflowStore(Arg.Any<ConnectionModel>());
        }

        [Test]
        public void Test_ConnectionSucceeds_ReturnsSuccess()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);
            _connectionValidator.Validate(Arg.Any<ConnectionViewModel>()).Returns(new ValidationResult());

            // set up the workflow store to throw an exception
            IWorkflowStore store = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(store);

            // execute
            var response = browser.Post(Actions.Connection.Test, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Count, Is.EqualTo(0));
            _workflowStoreFactory.Received(1).GetWorkflowStore(Arg.Any<ConnectionModel>());
            store.Received(1).GetIncompleteCount();
        }

        #endregion

        #region Private Methods

        private Browser CreateBrowser(UserIdentity currentUser)
        {
            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new ConnectionModule(_mapper, _cacheProvider, _userStore, _connectionValidator, _encryptionProvider, _workflowStoreService, _workflowStoreFactory))
                                .RootPathProvider(new TestRootPathProvider())
                                .RequestStartup((container, pipelines, context) => {
                                    context.CurrentUser = currentUser;
                                })
                            );
            return browser;
        }

        #endregion


    }
}
