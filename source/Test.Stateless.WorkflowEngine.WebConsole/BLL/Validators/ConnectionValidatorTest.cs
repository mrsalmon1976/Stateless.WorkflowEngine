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

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Count);
        }

        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        [TestCase(WorkflowStoreType.MongoDb, "   ")]
        public void Validate_InvalidHost_ReturnsFailure(WorkflowStoreType storeType, string host)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.Host = host;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            Assert.IsTrue(result.Messages[0].Contains("Host"));
        }

        [TestCase(WorkflowStoreType.MongoDb, 0)]
        [TestCase(WorkflowStoreType.MongoDb, -1)]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidPort_ReturnsFailure(WorkflowStoreType storeType, int? port)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.Port = port;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            Assert.IsTrue(result.Messages[0].Contains("Port"));
        }

        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "    ")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidDatabase_ReturnsFailure(WorkflowStoreType storeType, string database)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.Database = database;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            Assert.IsTrue(result.Messages[0].Contains("Database"));
        }

        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "   ")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidActiveCollection_ReturnsFailure(WorkflowStoreType storeType, string activeCollection)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.ActiveCollection = activeCollection;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            Assert.IsTrue(result.Messages[0].Contains("Active collection"));
        }


        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "   ")]
        [TestCase(WorkflowStoreType.MongoDb, null)]
        public void Validate_InvalidCompletedCollection_ReturnsFailure(WorkflowStoreType storeType, string completedCollection)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.CompletedCollection = completedCollection;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            Assert.IsTrue(result.Messages[0].Contains("Completed collection"));
        }

        [TestCase(WorkflowStoreType.MongoDb, null)]
        [TestCase(WorkflowStoreType.MongoDb, "")]
        [TestCase(WorkflowStoreType.MongoDb, "notthepassword")]
        public void Validate_PasswordDoesNotMatchConfirmPassword_ReturnsFailure(WorkflowStoreType storeType, string passwordConfirm)
        {
            ConnectionViewModel model = DataHelper.CreateConnectionViewModel(storeType);
            model.PasswordConfirm = passwordConfirm;

            ValidationResult result = _connectionValidator.Validate(model);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Count);
            Assert.IsTrue(result.Messages[0].Contains("Password and confirmation password do not match"));
        }

    }
}
