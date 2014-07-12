using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using NSubstitute;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Test.Stateless.WorkflowEngine.Workflows.SingleInstance;
using Stateless.WorkflowEngine.Models;
using Stateless.WorkflowEngine.Services;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowClientTest
    {

        #region IsSingleInstanceWorkflowRegistered Tests

        [Test]
        public void IsSingleInstanceWorkflowRegistered_OnExecute_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService);
            workflowClient.IsSingleInstanceWorkflowRegistered<BasicWorkflow>();
            regService.Received(1).IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);
        }

        #endregion

        #region RegisterWorkflow Tests

        [Test]
        public void RegisterWorkflow_OnRegister_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore, regService, Substitute.For<IWorkflowExceptionHandler>());
            workflowServer.RegisterWorkflow(workflow);

            regService.Received(1).RegisterWorkflow(workflowStore, workflow);

        }

        #endregion

    }
}
