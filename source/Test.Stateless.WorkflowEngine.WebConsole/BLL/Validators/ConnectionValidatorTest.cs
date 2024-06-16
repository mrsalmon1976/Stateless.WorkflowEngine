using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Validators
{
    [TestFixture]
    public class ConnectionValidatorTest
    {
        private IConnectionValidator _connectionValidator;

        [SetUp]
        public void ConnectionValidatorTest_SetUp()
        {
            _connectionValidator = new ConnectionValidator();
        }

        [TestCase(WorkflowStoreType.MongoDb)]
        public void Validate_IsValid_ReturnsSuccess(WorkflowStoreType storeType)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            
            ValidationResult result = _connectionValidator.Validate(model);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Count, Is.EqualTo(0));
        }

        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        [TestCase(WorkflowStoreType.MongoDb, "   ")]
        public void Validate_InvalidHost_ReturnsFailure(WorkflowStoreType storeType, string host)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.Host = host;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Host"), Is.True);
        }

        [TestCase(WorkflowStoreType.MongoDb, 0)]
        [TestCase(WorkflowStoreType.MongoDb, -1)]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidPort_ReturnsFailure(WorkflowStoreType storeType, int? port)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.Port = port;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Port"), Is.True);
        }

        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "    ")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidDatabase_ReturnsFailure(WorkflowStoreType storeType, string database)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.Database = database;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Database"), Is.True);
        }

        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "   ")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidActiveCollection_ReturnsFailure(WorkflowStoreType storeType, string activeCollection)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.ActiveCollection = activeCollection;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Active collection"), Is.True);
        }


        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "   ")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidCompletedCollection_ReturnsFailure(WorkflowStoreType storeType, string completedCollection)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.CompletedCollection = completedCollection;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Completed collection"), Is.True);
        }

        [TestCase(WorkflowStoreType.MongoDb, null)]
        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "notthepassword")]
        public void Validate_PasswordDoesNotMatchConfirmPassword_ReturnsFailure(WorkflowStoreType storeType, string passwordConfirm)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.PasswordConfirm = passwordConfirm;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Password and confirmation password do not match"), Is.True);
        }

    }
}
