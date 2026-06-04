Stateless.Workflow
==================

Stateless.Workflow is a basic .NET workflow engine based on the awesome [stateless](https://github.com/nblumhardt/stateless) State Machine. It works in the same fashion as Stateless, but provides a wrapper for moving between states with the extra features expected of a workflow engine, such a retry attempts, exception handling, delays between workflow steps, etc.

[![Build status](https://ci.appveyor.com/api/projects/status/vb99b6kidifjl1ir?svg=true)](https://ci.appveyor.com/project/mrsalmon1976/stateless-workflowengine)

# Persistence

The engine supports multiple data stores: 

 1. MemoryStore - this is an in-memory store: not particularly useful for most scenarios but it's good for testing, and could theoretically be used in an application wanting to take advantage of non-critical, simple workflowed steps in a background process.
 2. MongoDbStore - a store using a MongoDb database.
 3. RavenDb - a store using a RavenDb database.
 
All stores will use two collections: Workflows and CompletedWorkflows.  When a workflow is registered via the server or client, it is persisted in the Workflows collection.  The workflow will then move through various states until completion, in which case it will be moved to the CompletedWorkflows collection.  This store is never read by the workflow engine, it is purely for archiving purposes.

## Version Info

This table shows the client version and the associated driver/client versions used.

| Store     | Version   | Package        | Package Version |
|-----------|-----------|----------------|-----------------|
| MongoDb   | 2.6.0     | MongoDB.Driver | 2.13.2          |
|           | 3.0.0     | MongoDB.Driver | 2.19.0          |
|           | 3.1.0     | MongoDB.Driver | 2.21.0          |
|           | 4.0.0     | MongoDB.Driver | 2.26.0          |
|           | 5.0.0     | MongoDB.Driver | 2.30.0          |
| RavenDb   | 2.6.0     | RavenDB.Client | 3.5.7           |
|           | 3.0.0     | RavenDB.Client | 5.4.4           |
|           | 3.1.0     | RavenDB.Client | 5.4.109         |
|           | 4.0.0     | RavenDB.Client | 5.4.200         |
|           | 5.0.0     | RavenDB.Client | 7.2.1           |
|           | 5.1.0     | RavenDB.Client | 7.2.2           |

# Workflow Configuration

Workflows are configured in a similar fashion to Stateless.  You should make an effort to understand how Stateless works before attempting to use this workflow engine.  All configuration should go into the `Initialise` override on the Workflow itself.  Extra properties can be added to your custom workflows, and these will be serialized with the workflow when it is persisted to the store. The Stateless concepts of State and Triggers remain the same, and are used by the workflow engine to move between actions in the workflow.

# Example  

There is a full example application in the source code, in the Test.Stateless.WorkflowEngine.Example folder.  This contains the following examples:

1. register workflows with an `IWorkflowClient`
2. execute these workflows: each writes 10 files to the "C:\Temp" folder, then deletes all the files.  
3. create and execute 1000 workflows using sync methods with contrived exceptions to show the retry mechanism of the engine
4. same as previous, except using async methods 

You can look at this example to see how to set up a windows service that runs the workflow engine.  You can set up your connection properties in the Bootstrapper.

# Code and Concepts

## Creating a WorkflowServer

You will need a WorkflowServer, usually running in the context of a windows service, in order to process workflows in your application.

Note that this should be implemented in your application as a singleton, to prevent continuous schema checks which will slow down your application.

### Workflow Server Options

The `IWorkflowServer` class takes a `WorkflowServerOptions` parameter, where the following options can be set:

- `AutoCreateTables` - for workflow stores where tables must be explicitly created, if set to `true` this option will result in the server creating the tables if they do not exist.  Defaults to `true`.
- `AutoCreateIndexes` - if set to `true` this option will result in the server creating basic indexes (see below for more details).  Defaults to `true`.
- `PersistWorkflowDefinitions` - if set to true, workflow definitions will be inspected and stored if possible.  Defaults to `true`.

### Executing a Single Workflow

`IWorkflowServer` provides both synchronous and asynchronous methods for executing a single workflow instance directly:

| Method | Description |
|--------|-------------|
| `void ExecuteWorkflow(Workflow workflow)` | Synchronous. Fires the workflow's current trigger and saves state. |
| `Task ExecuteWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)` | Asynchronous equivalent. The cancellation token is forwarded through to the workflow's `FireAsync` call and into any `IWorkflowActionAsync` actions. |

You would typically use these methods only when you have a specific workflow instance already in hand (e.g. in tests or when reprocessing a suspended workflow). The batch methods below are the normal production path.

### Executing Workflows in Bulk

`IWorkflowServer` provides batch execution methods that load workflows from the store and execute them, with optional concurrency control:

| Method | Description |
|--------|-------------|
| `int ExecuteWorkflows(int count, int? maxConcurrent = null)` | Synchronous. Loads up to `count` workflows and executes them using `Parallel.ForEach`, with no more than `maxConcurrent` running at once. |
| `Task<int> ExecuteWorkflowsAsync(int count, int? maxConcurrent = null, CancellationToken? cancellationToken = null)` | Asynchronous. Loads up to `count` workflows and executes them using a `SemaphoreSlim` to cap concurrency at `maxConcurrent`. Supports cancellation — already-dispatched tasks run to completion before the method returns. |

Both methods load workflows ordered by **Priority DESC**, then **CreatedOn ASC**.

The following code loads up to 100 workflows and processes them 10 at a time:

```csharp
int workflowsExecuted = await _workflowServer.ExecuteWorkflowsAsync(100, 10);
```

The return value is the number of workflows actually dispatched. If there were only 20 active workflows ready to run it returns 20; if there were more than 100 it caps at 100. This is useful for deciding whether to sleep or loop immediately:

```csharp
while (true)
{
    int executed = await _workflowServer.ExecuteWorkflowsAsync(100, 10, cancellationToken);
    if (executed == 0)
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
}
```

#### Cancellation

`ExecuteWorkflowsAsync` accepts a `CancellationToken`. Cancelling it stops new workflows from being dispatched but allows any already-running workflows to finish cleanly. The token is also forwarded into each `ExecuteWorkflowAsync` call, and from there into `workflow.FireAsync` and any `IWorkflowActionAsync` actions, so long-running async actions can observe it directly.

## Creating a WorkflowClient

A WorkflowClient is used to register workflows for processing by the WorkflowServer.

## Workflow Actions

Each step of a workflow is implemented as an action class. There are two variants depending on whether the work is synchronous or asynchronous.

### Synchronous Actions

Implement `IWorkflowAction` and its `Execute(Workflow workflow)` method. Wire it up in your workflow's `Initialise` method using `OnEntry` and `ExecuteWorkflowAction<T>()`:

```csharp
public interface IWorkflowAction
{
    void Execute(Workflow workflow);
}
```

```csharp
// In your Workflow.Initialise():
this.Configure(State.WritingFirstFile)
    .OnEntry(() => this.ExecuteWorkflowAction<WriteFirstFileAction>())
    .Permit(Trigger.WriteAdditionalFiles, State.WritingAdditionalFiles);
```

```csharp
public class WriteFirstFileAction : IWorkflowAction
{
    public void Execute(Workflow workflow)
    {
        var wf = (FileCreationWorkflow)workflow;
        // do synchronous work...
        wf.ResumeTrigger = FileCreationWorkflow.Trigger.WriteAdditionalFiles.ToString();
    }
}
```

### Asynchronous Actions

Implement `IWorkflowActionAsync` and its `ExecuteAsync(Workflow workflow, CancellationToken cancellationToken)` method. Wire it up using `OnEntryAsync` and `ExecuteWorkflowActionAsync<T>()`:

```csharp
public interface IWorkflowActionAsync
{
    Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default);
}
```

```csharp
// In your Workflow.Initialise():
this.Configure(State.MarkingRecordAsProcessed)
    .OnEntryAsync(async () => await this.ExecuteWorkflowActionAsync<MarkRecordAsProcessedAsyncAction>())
    .Permit(Trigger.Complete, State.Complete);
```

```csharp
public class MarkRecordAsProcessedAsyncAction : IWorkflowActionAsync
{
    public async Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var wf = (VolumeAsyncWorkflow)workflow;
        // await async work...
        wf.ResumeTrigger = VolumeAsyncWorkflow.Trigger.Complete.ToString();
    }
}
```

The `CancellationToken` passed to `ExecuteWorkflowActionAsync<T>` flows from `WorkflowServer.ExecuteWorkflowsAsync`, so if the server's token is cancelled your action can observe it and stop cleanly.

### Action Hooks

Both sync and async paths provide hook methods on the `Workflow` base class that you can override for logging or instrumentation:

| Sync | Async | Fires |
|------|-------|-------|
| `OnActionExecuting(IWorkflowAction action)` | `OnActionExecutingAsync(IWorkflowActionAsync action, CancellationToken ct)` | Before the action executes |
| `OnActionExecuted(IWorkflowAction action)` | `OnActionExecutedAsync(IWorkflowActionAsync action, CancellationToken ct)` | After the action executes |

### Choosing Sync vs Async

Use `IWorkflowActionAsync` whenever the action performs I/O (database queries, HTTP calls, file operations). Use `IWorkflowAction` for CPU-bound or trivially fast work. Mixing both in the same workflow is fine — each state entry independently chooses its variant.

## Workflow Priority

The workflow engine will process workflows in the order that they come in.  If a workflow fails, it will go to the back of the line, but will still get processed in date order.  In some cases, you may want particular workflows within a single workflow collection to be processed as a higher priority.  

For example, you may have an email handler that processes emails, but when users register they should be sent an email immediately.  In this case, you may want to prioritise registration emails over any other emails.

To cater for this, workflows have a "Priority" property that can be set.  This defaults to 0, but the higher the value, the more important the workflow is.  Workflows will get processed in order of priority (descending) before date.

## Dependency Injection

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
## Events

Workflows move through states, but there are also events that occur in the lifecycle that are raised, allowing you to take 
action.

### WorkflowServer

The `IWorkflowServer` interface exposes two events:

1.` WorkflowSuspended` - this event is raised by the WorkflowServer when a workflow suspends.  This means that a workflow has errored repeatedly until the maximum number of retries (RetryCount) defined for the workflow has been exceeded.  The workflow goes into a suspended state and will not be picked up again by the WorkflowEngine until the problems have been resolved.  
2. `WorkflowCompleted` - this event is raised when a workflow completes.  The event is fired AFTER the workflow is archived and the workflow.OnComplete() method is called.  

### Workflow

When you implement your `Workflow` classes, you can also override the following event handlers:

1. `OnActionExecuting(IWorkflowAction action)` - Invoked before a synchronous workflow action executes. Useful for logging.
2. `OnActionExecutingAsync(IWorkflowActionAsync action, CancellationToken ct)` - Async equivalent, invoked before an async workflow action executes.
3. `OnActionExecuted(IWorkflowAction action)` - Invoked after a synchronous workflow action executes. Useful for logging.
4. `OnActionExecutedAsync(IWorkflowActionAsync action, CancellationToken ct)` - Async equivalent, invoked after an async workflow action executes.
5. `OnComplete` - Invoked when a workflow completes and is moved into the CompletedWorkflows collection.
6. `OnError` - Invoked when an error occurs within a workflow action. Useful for error logging.
7. `OnSuspend` - Invoked when a workflow has exceeded its configured retries, and moves into a suspended state.

## Single Instance Workflows

The engine supports the concept of a "Single Instance" workflows, which means there can only ever be one workflow of this type.  If you try to register a single instance workflow and one already exists, and exception will be thrown by the engine.

You would usually only register single instance workflows on application start up.  Both `IWorkflowClient` and `IWorkflowServer` expose methods to check if an instance exists already.  Example code would look like this:

```csharp
if (!_workflowServer.IsSingleInstanceWorkflowRegistered<MyWorkflow>()) 
{
  _workflowServer.RegisterWorkflowType<MyWorkflow>();
}
```

# Workflow Stores

## Indexing on data stores

Over time, workflow collections grow, and the CompletedWorkflows data store can get very large.  By default, instantiation of the WorkflowServer class will call the Initialise() method on the WorkflowStore being used, which will create the necessary tables/collections and indexes.  This behavior can be removed by setting the relevant values on the WorkflowServerOptions parameter on the constructor

## Default Indexes

The following indexes are created by default:

* Workflows: Priority descending, RetryCount descending, CreatedOn ascending
* CompletedWorkflows: CreatedOn descending

## Manually creating indexes: MongoDb

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

To remove this dependency, you can add the following attribute to your workflow class definition to fix it on only your worklow.  Note that child classes will also need this attribute.

```csharp
[MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
```

Alternatively, you can fix it globally in application start-up:

```csharp
ConventionRegistry.Register(
    "IgnoreExtraElements",
    new ConventionPack { new IgnoreExtraElementsConvention(true) },
    type => true);
```
Note that like all things, this comes with its own risks - the engine will no longer fail to load a document with a property it does not recognise, but you will lose this data when it is written back to the document store after execution.

# Workflow Definitions


The `IWorkflowServer` will attempt to persist workflow definitions into a `WorkflowDefinitions` collection or table when types are registered.  These will result in graphs that can be rendered in the console displaying a representation of the workflow.  This option can be turned off using the `WorkflowOptions` property.
