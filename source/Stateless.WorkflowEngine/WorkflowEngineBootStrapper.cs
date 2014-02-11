using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Stores;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Stateless.WorkflowEngine
{

    public enum WorkflowStoreType
    {
        InMemory = 0,
        RavenDb = 1,
        MongoDb = 2,
    }

    public class WorkflowEngineBootStrapper
    {

        public static void Boot(WorkflowStoreType storeType)
        {
            ObjectFactory.Configure(x => x.For<IWorkflowExceptionHandler>().Use<WorkflowExceptionHandler>());
            ObjectFactory.Configure(x => x.For<IWorkflowClient>().Use<WorkflowClient>());
            ObjectFactory.Configure(x => x.For<IWorkflowServer>().Use<WorkflowServer>());
            ObjectFactory.Configure(x => x.For<IWorkflowRegistrationService>().Use<WorkflowRegistrationService>());

            switch (storeType)
            {
                case WorkflowStoreType.InMemory:
                    ObjectFactory.Configure(x => x.For<IWorkflowStore>().Use<MemoryWorkflowStore>());
                    break;
                case WorkflowStoreType.RavenDb:
                    ObjectFactory.Configure(x => x.For<IWorkflowStore>().Use<RavenDbWorkflowStore>());
                    break;
                case WorkflowStoreType.MongoDb:
                    ObjectFactory.Configure(x => x.For<IWorkflowStore>().Use<MongoDbWorkflowStore>());
                    break;

            }
            
        }
    }
}
