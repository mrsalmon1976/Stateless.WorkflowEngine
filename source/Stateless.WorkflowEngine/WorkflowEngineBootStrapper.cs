using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Stores;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine
{
    public class WorkflowEngineBootStrapper
    {
        public static void Boot()
        {
            ObjectFactory.Configure(x => x.For<IWorkflowClient>().Use<WorkflowClient>());
            ObjectFactory.Configure(x => x.For<IWorkflowServer>().Use<WorkflowServer>());
            ObjectFactory.Configure(x => x.For<IWorkflowRegistrationService>().Use<WorkflowRegistrationService>());
            ObjectFactory.Configure(x => x.For<IWorkflowStore>().Use<RavenDbWorkflowStore>());
        }
    }
}
