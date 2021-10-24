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
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
                else
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
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
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            Assert.AreEqual(0, _userStore.Connections.Count);
            Assert.AreEqual(0, connections.Count);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            _workflowStoreService.DidNotReceive().GetWorkflowStoreInfo(Arg.Any<ConnectionModel>());
            _cacheProvider.Received(1).Get<ConnectionInfoViewModel>(cacheKey);

            ConnectionInfoViewModel result = JsonConvert.DeserializeObject<ConnectionInfoViewModel>(response.Body.AsString());
            Assert.AreEqual(cachedResult.ActiveCount, result.ActiveCount);
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
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            _workflowStoreService.Received(1).GetWorkflowStoreInfo(conn);

            ConnectionInfoViewModel result = JsonConvert.DeserializeObject<ConnectionInfoViewModel>(response.Body.AsString());
            Assert.AreEqual(infoViewModel.ActiveCount, result.ActiveCount);
            Assert.AreEqual(infoViewModel.SuspendedCount, result.SuspendedCount);
            Assert.AreEqual(infoViewModel.CompleteCount, result.CompleteCount);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

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
            Assert.IsNotNull(model);
            Assert.AreEqual(model.Connections[0].Host, "a");
            Assert.AreEqual(model.Connections[0].Database, "A");
            Assert.AreEqual(model.Connections[1].Host, "A");
            Assert.AreEqual(model.Connections[1].Database, "b");
            Assert.AreEqual(model.Connections[2].Host, "y");
            Assert.AreEqual(model.Connections[2].Database, "B");
            Assert.AreEqual(model.Connections[3].Host, "Y");
            Assert.AreEqual(model.Connections[3].Database, "z");
            Assert.AreEqual(model.Connections[4].Host, "Z");
            Assert.AreEqual(model.Connections[4].Database, "A");
            Assert.AreEqual(model.Connections[5].Host, "Z");
            Assert.AreEqual(model.Connections[5].Database, "B");
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
            Assert.IsTrue(model.CurrentUserCanDeleteConnection);
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
            Assert.IsFalse(model.CurrentUserCanDeleteConnection);
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
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
                else
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
            _encryptionProvider.Received(1).SimpleEncrypt(password, key, null);

            Assert.AreEqual(1, _userStore.Connections.Count);
            Assert.AreEqual(encryptedPassword, _userStore.Connections[0].Password);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);

            Assert.AreEqual(1, _userStore.Connections.Count);
            Assert.AreNotEqual(Guid.Empty, _userStore.Connections[0].Id);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
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
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            ValidationResult result = JsonConvert.DeserializeObject<ValidationResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
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
