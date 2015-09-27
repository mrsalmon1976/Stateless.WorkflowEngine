using NUnit.Framework;
using Stateless.WorkflowEngine.UI.Console.AppCode.Services;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NSubstitute;
using Stateless.WorkflowEngine.UI.Console.AppCode.Providers;
using Stateless.WorkflowEngine.UI.Console.AppCode.Factories;
using Stateless.WorkflowEngine.UI.Console.AppCode.Models.Workflow;
using System.Threading;
using Stateless.WorkflowEngine.UI.Console.Services.Workflow;

namespace Test.Stateless.WorkflowEngine.UI.Console.AppCode.Services
{
    [TestFixture]
    public class UIConnectionServiceTest
    {
        [Test]
        public void RunAsyncConnection_ExceptionOccurs_ResultIsException()
        {
            WorkflowStoreConnection conn = Substitute.For<WorkflowStoreConnection>();
            UserSettings userSettings = Substitute.For<UserSettings>();
            IWorkflowProviderFactory providerFactory = Substitute.For<IWorkflowProviderFactory>();
            ConnectionResult result = null;
            string exceptionMsg = Guid.NewGuid().ToString();

            // make sure an exception is thrown
            providerFactory.When(x => x.GetWorkflowService(Arg.Any<WorkflowStoreConnection>()))
                .Do(x => { throw new Exception(exceptionMsg); });


            // set up a callback that just sets the local variable
            Action<ConnectionResult> callback = (ConnectionResult connResult) => 
            {
                result = connResult;
            };

            // run the async method
            UIConnectionService service = new UIConnectionService(userSettings, providerFactory);
            service.RunAsyncConnection(conn, callback);

            // now wait until the result comes back
            int i = 0;
            while (i < 30)
            {
                if (result != null) break;
                Thread.Sleep(100);
                i++;
            }

            // check the result
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Exception);
            Assert.IsNull(result.WorkflowProvider);
            Assert.AreEqual(exceptionMsg, result.Exception.Message);
        }

        [Test]
        public void RunAsyncConnection_OnSuccessfulConnection_ProviderReturned()
        {
            WorkflowStoreConnection conn = Substitute.For<WorkflowStoreConnection>();
            UserSettings userSettings = Substitute.For<UserSettings>();
            
            IWorkflowProvider workflowProvider = Substitute.For<IWorkflowProvider>();
            IWorkflowProviderFactory providerFactory = Substitute.For<IWorkflowProviderFactory>();
            providerFactory.GetWorkflowService(conn).Returns(workflowProvider);

            ConnectionResult result = null;

            // set up a callback that just sets the local variable
            Action<ConnectionResult> callback = (ConnectionResult connResult) =>
            {
                result = connResult;
            };

            // run the async method
            UIConnectionService service = new UIConnectionService(userSettings, providerFactory);
            service.RunAsyncConnection(conn, callback);

            // now wait until the result comes back
            int i = 0;
            while (i < 30)
            {
                if (result != null) break;
                Thread.Sleep(100);
                i++;
            }

            // check the result
            Assert.IsNotNull(result);
            Assert.IsNull(result.Exception);
            Assert.IsNotNull(result.WorkflowProvider);

            providerFactory.Received(1).GetWorkflowService(conn);
            workflowProvider.Received(1).GetActive(1);
            Assert.AreEqual(workflowProvider, result.WorkflowProvider);
        }

        [Test]
        public void RunAsyncConnection_NewConnection_ConnectionSaved()
        {
            WorkflowStoreConnection conn = Substitute.For<WorkflowStoreConnection>();
            UserSettings userSettings = Substitute.For<UserSettings>();

            IWorkflowProvider workflowProvider = Substitute.For<IWorkflowProvider>();
            workflowProvider.Connection.Returns(conn);
            IWorkflowProviderFactory providerFactory = Substitute.For<IWorkflowProviderFactory>();
            providerFactory.GetWorkflowService(conn).Returns(workflowProvider);

            ConnectionResult result = null;

            // set up a callback that just sets the local variable
            Action<ConnectionResult> callback = (ConnectionResult connResult) =>
            {
                result = connResult;
            };

            // run the async method
            UIConnectionService service = new UIConnectionService(userSettings, providerFactory);
            service.RunAsyncConnection(conn, callback);

            // now wait until the result comes back
            int i = 0;
            while (i < 30)
            {
                if (result != null) break;
                Thread.Sleep(100);
                i++;
            }

            // check the result
            Assert.IsNotNull(result.WorkflowProvider);

            userSettings.Received(1).Save();
            Assert.Contains(workflowProvider.Connection, userSettings.Connections);
        }

        [Test]
        public void RunAsyncConnection_ExistingConnection_ConnectionNotSaved()
        {
            WorkflowStoreConnection conn = Substitute.For<WorkflowStoreConnection>();
            UserSettings userSettings = Substitute.For<UserSettings>();
            userSettings.Connections.Add(conn);

            IWorkflowProvider workflowProvider = Substitute.For<IWorkflowProvider>();
            workflowProvider.Connection.Returns(conn);
            IWorkflowProviderFactory providerFactory = Substitute.For<IWorkflowProviderFactory>();
            providerFactory.GetWorkflowService(conn).Returns(workflowProvider);

            ConnectionResult result = null;

            // set up a callback that just sets the local variable
            Action<ConnectionResult> callback = (ConnectionResult connResult) =>
            {
                result = connResult;
            };

            // run the async method
            UIConnectionService service = new UIConnectionService(userSettings, providerFactory);
            service.RunAsyncConnection(conn, callback);

            // now wait until the result comes back
            int i = 0;
            while (i < 30)
            {
                if (result != null) break;
                Thread.Sleep(100);
                i++;
            }

            // check the result
            Assert.IsNotNull(result.WorkflowProvider);

            userSettings.DidNotReceive().Save();
        }

    }
}
