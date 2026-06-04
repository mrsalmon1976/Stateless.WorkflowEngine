using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System;

namespace Test.Stateless.WorkflowEngine.WebConsole.ViewModels.Connection
{
    [TestFixture]
    public class ConnectionMappingExtensionsTest
    {
        #region ToConnectionModel Tests

        [Test]
        public void ToConnectionModel_MapsAllProperties()
        {
            ConnectionViewModel viewModel = new ConnectionViewModel
            {
                Id = Guid.NewGuid(),
                WorkflowStoreType = WorkflowStoreType.MongoDb,
                Host = "localhost",
                Database = "TestDb",
                User = "admin",
                Password = "secret",
                PasswordConfirm = "secret",
                Port = 27017,
                ReplicaSet = "rs0",
                ActiveCollection = "Workflows",
                CompletedCollection = "CompletedWorkflows",
            };

            ConnectionModel result = viewModel.ToConnectionModel();

            Assert.That(result.Id, Is.EqualTo(viewModel.Id));
            Assert.That(result.WorkflowStoreType, Is.EqualTo(viewModel.WorkflowStoreType));
            Assert.That(result.Host, Is.EqualTo(viewModel.Host));
            Assert.That(result.Database, Is.EqualTo(viewModel.Database));
            Assert.That(result.User, Is.EqualTo(viewModel.User));
            Assert.That(result.Password, Is.EqualTo(viewModel.Password));
            Assert.That(result.Port, Is.EqualTo(viewModel.Port));
            Assert.That(result.ReplicaSet, Is.EqualTo(viewModel.ReplicaSet));
            Assert.That(result.ActiveCollection, Is.EqualTo(viewModel.ActiveCollection));
            Assert.That(result.CompletedCollection, Is.EqualTo(viewModel.CompletedCollection));
        }

        #endregion

        #region ToConnectionViewModel Tests

        [Test]
        public void ToConnectionViewModel_MapsAllProperties()
        {
            ConnectionModel model = new ConnectionModel
            {
                Id = Guid.NewGuid(),
                WorkflowStoreType = WorkflowStoreType.MongoDb,
                Host = "localhost",
                Database = "TestDb",
                User = "admin",
                Port = 27017,
                ReplicaSet = "rs0",
                ActiveCollection = "Workflows",
                CompletedCollection = "CompletedWorkflows",
            };

            ConnectionViewModel result = model.ToConnectionViewModel();

            Assert.That(result.Id, Is.EqualTo(model.Id));
            Assert.That(result.WorkflowStoreType, Is.EqualTo(model.WorkflowStoreType));
            Assert.That(result.Host, Is.EqualTo(model.Host));
            Assert.That(result.Database, Is.EqualTo(model.Database));
            Assert.That(result.User, Is.EqualTo(model.User));
            Assert.That(result.Port, Is.EqualTo(model.Port));
            Assert.That(result.ReplicaSet, Is.EqualTo(model.ReplicaSet));
            Assert.That(result.ActiveCollection, Is.EqualTo(model.ActiveCollection));
            Assert.That(result.CompletedCollection, Is.EqualTo(model.CompletedCollection));
        }

        [Test]
        public void ToConnectionViewModel_DoesNotMapPassword()
        {
            ConnectionModel model = new ConnectionModel { Password = "secret" };

            ConnectionViewModel result = model.ToConnectionViewModel();

            Assert.That(result.Password, Is.Null);
        }

        [Test]
        public void ToConnectionViewModel_DoesNotMapPasswordConfirm()
        {
            ConnectionModel model = new ConnectionModel();

            ConnectionViewModel result = model.ToConnectionViewModel();

            Assert.That(result.PasswordConfirm, Is.Null);
        }

        #endregion
    }
}
