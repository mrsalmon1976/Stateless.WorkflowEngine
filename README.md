Stateless.Workflow
==================

Stateless.Workflow is a basic .NET workflow engine based on the awesome [stateless](https://github.com/nblumhardt/stateless) State Machine. It works in the same fashion as Stateless, but provides a wrapper for moving between states with the extra features expected of a workflow engine, such a retry attempts, exception handling, delays between workflow steps, etc.

[![Build status](https://ci.appveyor.com/api/projects/status/vb99b6kidifjl1ir?svg=true)](https://ci.appveyor.com/project/mrsalmon1976/stateless-workflowengine)

## Persistence

The engine supports multiple data stores: 

 1. MemoryStore - this is an in-memory store: not particularly useful for most scenarios but it's good for testing, and could theoretically be used in an application wanting to take advantage of non-critical, simple workflowed steps in a background process.
 2. MongoDbStore - a store using a MongoDb database.
 3. RavenDb - a store using a RavenDb database.
 
All stores will use two collections: Workflows and CompletedWorkflows.  When a workflow is registered via the server or client, it is persisted in the Workflows collection.  The workflow will then move through various states until completion, in which case it will be moved to the CompletedWorkflows collection.  This store is never read by the workflow engine, it is purely for archiving purposes.

## Workflow Configuration

Workflows are configured in similar fashion to Stateless.  You should make an effort to understand how Stateless works before attempting to use this workflow engine.  All configuration should go into the Initialise override on the Workflow itself.  Extra properties can be added to your custom workflows, and these will be serialized with the workflow when it is persisted to the store. The Stateless concepts of State and Triggers remain the same, and are used by the workflow engine to move between actions in the workflow.

## Example  

There is a full example application in the source code, in the Test.Stateless.WorkflowEngine.Example folder.  This contains a windows service that creates a sample workflow when it runs, which writes 10 files to the "C:\Temp" folder, then deletes all the files.  You can look at this example to see how to set up a windows service that runs the workflow engine.  The WorkerThreadFunc method in the WorkflowEngineExampleService class contains an instantiation of each store type (Memory, MongoDb and RavenDb), if you'd like to run the example just configure what you want.  You can set up your connection properties in the Bootstrapper.

## Code

## Creating a WorkflowServer

You will need a WorkflowServer, usually running in the context of a windows service, in order to process workflows in your application.

Note that this should be implemented in your application as a singleton, to prevent continuous schema checks which will slow down your application.

## Creating a WorkflowClient

A WorkflowClient is used to register workflows for processing by the WorkflowServer.  

### Workflow Priority

The workflow engine will process workflows in the order that they come in.  If a workflow fails, it will go to the back of the line, but will still get processed in date order.  In some cases, you may want particular workflows within a single workflow collection to be processed as a higher priority.  

For example, you may have an email handler that processes emails, but when users register they should be sent an email immediately.  In this case, you may want to prioritise registration emails over any other emails.

To cater for this, workflows have a "Priority" property that can be set.  This defaults to 0, but the higher the value, the more important the workflow is.  Workflows will get processed in order of priority (descending) before date.

### Dependency Injection

Most of the classes in the code implement an interface, which allows for the inversion of control.  One exception to this is 
the **_Workflow_** class, which needed to be concrete class.  This class contains the method to create a workflow action, using 
Activator.CreateInstance() which isn't great for DI.  For this purpose, the class provides the 

```csharp
CreateWorkflowActionInstance<T>()
```

method which can be overridden if you want to take control over how workflow actions are instantiated.

As of version 1.3.0, however, there is a better option.  The `WorkflowServer` class (required for the execution of workflows), has a `DependencyResolver` property, that implements the `IWorkflowEngineDependencyResolver` interface.  This can be set as part of your application's bootstrapping process:

```csharp
MyDependencyResolver resolver = new MyDependencyResolver();
workflowServer.DependencyResolver = resolver;
```

The interface has one method: `T GetInstance<T>` - which allows you to use your own DI framework to instantiate the action class.

In .NET6, you can add the following code in your ConfigureServices() method - just make sure it's at the end of the method so it gets the benefit of the configured services!

```csharp
services.AddSingleton<IWorkflowServer>((sp) => 
{
    var store = sp.GetService<IMongoDbWorkflowProvider>().GetStore();
    var workflowServer = new WorkflowServer(store);
    workflowServer.DependencyResolver = new MyResolver(services.BuildServiceProvider());
    return workflowServer;
});
```

Your resolver would look like this:

```csharp
private class MyResolver : IWorkflowEngineDependencyResolver
{
  private readonly IServiceProvider _serviceProvider;

  public MyResolver(IServiceProvider serviceProvider)
  {
    this._serviceProvider = serviceProvider;
  }

  public T GetInstance<T>() where T : class
  {
    var result = _serviceProvider.GetService<T>();
    if (result == null)
    {
      throw new Exception("Class " + typeof(T).ToString() + " has not been registered with the container");
    }
    return result;
  }
}
```
### Events

Workflows move through states, but there are also events that occur in the lifecycle that are raised, allowing you to take 
action.

1. WorkflowSuspended - this event is raised by the WorkflowServer when a workflow suspends.  This means that a workflow has errored repeatedly 
until the maximum number of retries (RetryCount) defined for the workflow has been exceeded.  The workflow goes into a suspended state and will 
not be picked up again by the WorkflowEngine until the problems have been resolved.  Note that the Workflow itself has an OnSuspend method that 
can be overridden at the workflow level. 
2. WorkflowCompleted - this event is raised when a workflow completes.  The event is fired AFTER the workflow is archived and the workflow.OnComplete() 
method is called.  The workflow.OnComplete() method can also be overridden and actioned at the workflow level instead of using this event.

## Indexing on data stores

Over time, workflow collections grow, and the CompletedWorkflows data store can get very large.  By default, instantiation of the WorkflowServer class will call the Initialise() method on the WorkflowStore being used, which will create the necessary tables/collections and indexes.  This behavior can be removed by setting the relevant values on the WorkflowServerOptions parameter on the constructor

## Default Indexes

The following indexes are created by default:

* Workflows: Priority descending, RetryCount descending, CreatedOn ascending
* CompletedWorkflows: CreatedOn descending

### Manually creating indexes: MongoDb

On the CompletedWorkflows collection:

```csharp
db.CompletedWorkflows.createIndex({ "Workflow.CreatedOn" : -1 }, { name : "CompletedWorkflow_CreatedOn" })
```

On the Workflows collection (only create this if you generate a lot of workflows - usually your active workflow count should stay low so an index is not necessary):

```csharp
db.Workflows.createIndex({ "Workflow.Priority" : -1, "Workflow.RetryCount" : -1, "Workflow.CreatedOn" : 1 }, { name : "Workflow_Priority_RetryCount_CreatedOn" })
```

## Serialisation

### MongoDb

By default, when a class is deserialised, an exception will be thrown if a property exists on the document that is not declared on a workflow.  This can cause dependency issues in a distributed system - if one service registers a workflow and another service executes that workflow, if the definition changes you will need to deploy both services.  This gets particularly problematic when a workflow uses a class as a property that is used in multiple places.

To remove this dependency, you can add the following attribute to your workflow class definition:

```csharp
[MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
```

Note that like all things, this comes with its own risks - the engine will no longer fail to load a document with a property it does not recognise, but you will lose this data when it is written back to the document store after execution.
