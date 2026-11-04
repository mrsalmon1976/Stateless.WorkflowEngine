Stateless.Workflow
==================

Stateless.Workflow is a basic .NET workflow engine based on the awesome [stateless](https://github.com/nblumhardt/stateless) State Machine. It works in the same fashion as Stateless, but provides a wrapper for moving between states with the extra features expected of a workflow engine, such a retry attempts, exception handling, delays between workflow steps, etc.

[![Build status](https://ci.appveyor.com/api/projects/status/vb99b6kidifjl1ir?svg=true)](https://ci.appveyor.com/project/mrsalmon1976/stateless-workflowengine)

# Persistence

The engine supports multiple data stores: 

 1. MemoryStore - this is an in-memory store: not particularly useful for most scenarios but it's good for testing, and could theoretically be used in an application wanting to take advantage of non-critical, simple workflowed steps in a background process.
 2. MongoDbStore - a store using a MongoDb database.
 3. RavenDb - a store using a RavenDb database.
 
All stores will use two collections: Workflows and CompletedWorkflows.  When a workflow is registered via the server or client, it is persisted in the Workflows collection.  The workflow will then move through various states until completion, in which case it will be moved to the CompletedWorkflows collection.  This store is never read by the workflow engine, it is purely for archiving purposes.  A third collection, WorkflowNodes, holds the workflow servers that are currently running (see [Running Multiple Servers](#running-multiple-servers)).

## Version Info

This table shows the client version and the associated driver/client versions used.

| Store     | Version   | Package        | Package Version |
|-----------|-----------|----------------|-----------------|
| MongoDb   | 2.6.0     | MongoDB.Driver | 2.13.2          |
|           | 3.0.0     | MongoDB.Driver | 2.19.0          |
|           | 3.1.0     | MongoDB.Driver | 2.21.0          |
|           | 4.0.0     | MongoDB.Driver | 2.26.0          |
|           | 5.0.0     | MongoDB.Driver | 2.30.0          |
|           | 6.0.0     | MongoDB.Driver | 3.13.0          |
| RavenDb   | 2.6.0     | RavenDB.Client | 3.5.7           |
|           | 3.0.0     | RavenDB.Client | 5.4.4           |
|           | 3.1.0     | RavenDB.Client | 5.4.109         |
|           | 4.0.0     | RavenDB.Client | 5.4.200         |
|           | 5.0.0     | RavenDB.Client | 7.2.1           |
|           | 5.1.0     | RavenDB.Client | 7.2.2           |
|           | 6.0.0     | RavenDB.Client | 7.2.7           |

From version 6.0, all packages target **.NET 10** (`net10.0`).  Version 5.x targeted .NET Standard 2.0 (and .NET Framework 4.8 for RavenDb) - applications running on .NET Framework should stay on 5.x.

Version 6.0 replaces `IWorkflowEngineDependencyResolver` with `IServiceProvider` - see [Dependency Injection](#dependency-injection).

Version 6.0 of the MongoDb store uses version 3 of the MongoDb driver, which requires **MongoDb Server 4.4 or later** - stay on 5.x for older servers.  Applications using the store must also use version 3 of the driver (see the [driver's upgrade guide](https://www.mongodb.com/docs/drivers/csharp/current/reference/upgrade/v3/) for any changes needed in your own MongoDb code), and existing workflows remain readable (see [Guids](#guids)).

# Workflow Configuration

Workflows are configured in a similar fashion to Stateless.  You should make an effort to understand how Stateless works before attempting to use this workflow engine.  All configuration should go into the `Initialise` override on the Workflow itself.  Extra properties can be added to your custom workflows, and these will be serialized with the workflow when it is persisted to the store. The Stateless concepts of State and Triggers remain the same, and are used by the workflow engine to move between actions in the workflow.

# Example  

There is a full example console application in the source code, in the `source/Example` folder (`dotnet run --project source/Example`).  This contains the following examples:

1. register workflows with an `IWorkflowClient` (MongoDb or RavenDb)
2. execute these workflows with an `IWorkflowServer`: each writes 10 files to the "C:\Temp" folder, then deletes all the files.  
3. create and execute 1000 workflows using async methods with contrived exceptions to show the retry mechanism of the engine
4. same as previous, except using sync methods 
5. the multi-node stress test, which runs several workflow servers against the same store and proves that no workflow step is ever executed by two servers at the same time - see [Multi-node stress test](#multi-node-stress-test)

Every example sets up the engine with `AddStatelessWorkflowEngine` and logs through `ILogger` (`Shared/ExampleServices.cs`); the high volume examples use a scoped database connection in their actions (`Shared/VolumeDb.cs`).

The examples connect to MongoDb on `mongodb://localhost:27017` and RavenDb on `http://localhost:8080` - the containers started by `start.ps1` (see [Development](#development)).  These can be overridden with the `STATELESS_MONGODB_CONNECTIONSTRING` and `STATELESS_RAVENDB_URL` environment variables, or in `AppSettings.cs`.

# Getting Started

An application using the engine has two parts:

* a **WorkflowClient**, used wherever workflows are created (e.g. a web site or API) to register them in the workflow store
* a **WorkflowServer**, usually running as a Windows service, which picks up registered workflows and executes them

Both use the same workflow store, and need a reference to the assembly containing your workflow classes.  The steps below create a workflow that sends a welcome email, register it from your application, and run it from a Windows service.

## 1. Install the packages

Install the package for your workflow store (it includes the core `Stateless.WorkflowEngine` package):

```bash
dotnet add package Stateless.WorkflowEngine.MongoDb
# or
dotnet add package Stateless.WorkflowEngine.RavenDb
```

## 2. Create a workflow

A workflow is a class inheriting from `StateWorkflow<TState, TTrigger>`, which configures its [Stateless](https://github.com/dotnet-state-machine/stateless) states and triggers in `Initialise`.  Each step's work is done by a workflow action, and the action sets the `ResumeTrigger` - the trigger the server fires next.  The workflow is finished when `IsComplete` is set.

```csharp
using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine;

[BsonIgnoreExtraElements]   // MongoDb only - see Serialisation
public class WelcomeEmailWorkflow : StateWorkflow<WelcomeEmailWorkflow.State, WelcomeEmailWorkflow.Trigger>
{
    public enum State { Start, SendingEmail, Complete }

    public enum Trigger { SendEmail, Complete }

    // the parameterless constructor is used when the workflow is loaded from the store
    public WelcomeEmailWorkflow() : base(State.Start, Trigger.SendEmail)
    {
    }

    // custom properties are saved with the workflow
    public string? EmailAddress { get; set; }

    public override void Initialise(string initialState)
    {
        base.Initialise(initialState);

        this.Configure(State.Start)
            .Permit(Trigger.SendEmail, State.SendingEmail);

        this.Configure(State.SendingEmail)
            .OnEntryAsync(async () => await this.ExecuteWorkflowActionAsync<SendWelcomeEmailAction>())
            .Permit(Trigger.Complete, State.Complete);

        this.Configure(State.Complete)
            .OnEntry(() => this.IsComplete = true);
    }
}
```

```csharp
public class SendWelcomeEmailAction : IWorkflowActionAsync
{
    private readonly IEmailSender _emailSender;

    public SendWelcomeEmailAction(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public async Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var wf = (WelcomeEmailWorkflow)workflow;
        await _emailSender.SendAsync(wf.EmailAddress!, "Welcome!", cancellationToken);

        // tell the server which trigger to fire next
        wf.ResumeTrigger = WelcomeEmailWorkflow.Trigger.Complete.ToString();
    }
}
```

If an action throws, the workflow stays in its current state and is retried after each of its `RetryIntervals` (5, 10, 15, 30 and 60 seconds by default).  If it still fails, the workflow is suspended until someone unsuspends it - e.g. in the [web console](#web-console).

## 3. Register workflows

Register a workflow with a `WorkflowClient` wherever it should start - it is saved to the store, and the server will pick it up.  In an application that uses dependency injection (e.g. ASP.NET Core), add the engine with the same store as the server, and inject `IWorkflowClient`:

```csharp
// Program.cs
builder.Services.AddStatelessWorkflowEngine()
    .WithMongoDbStore(builder.Configuration.GetConnectionString("Workflows"), "MyApp");

// wherever the workflow starts
await workflowClient.RegisterAsync(new WelcomeEmailWorkflow { EmailAddress = user.EmailAddress });
```

Without dependency injection, create the store and client yourself:

```csharp
IWorkflowStore store = new MongoDbWorkflowStore(new MongoClient(connectionString).GetDatabase("MyApp"));
IWorkflowClient workflowClient = new WorkflowClient(store);
```

## 4. Run the WorkflowServer as a Windows service

The easiest way to host the server is a .NET [Worker Service](https://learn.microsoft.com/dotnet/core/extensions/workers), which runs as a Windows service with a few lines of code (and as a normal console app when run directly, which is handy for development):

```bash
dotnet new worker -n MyWorkflowServer
cd MyWorkflowServer
dotnet add package Microsoft.Extensions.Hosting.WindowsServices
dotnet add package Stateless.WorkflowEngine.MongoDb
```

Add a reference to the project containing your workflows, and replace `Program.cs` with the following. `AddStatelessWorkflowEngine` registers the server and client as singletons, the store is added with `WithMongoDbStore`, and the server creates workflow actions from the service provider, each in its own scope (see [Dependency Injection](#dependency-injection)):

```csharp
using Stateless.WorkflowEngine;

var builder = Host.CreateApplicationBuilder(args);

// runs as a Windows service when installed as one, or as a console app when run directly
builder.Services.AddWindowsService(options => options.ServiceName = "MyWorkflowServer");

builder.Services.AddStatelessWorkflowEngine()
    .WithMongoDbStore(builder.Configuration.GetConnectionString("Workflows"), "MyApp")
    // register each workflow type the server runs
    .AddWorkflowType<WelcomeEmailWorkflow>()
    // register the workflow actions, so missing dependencies are found at startup
    .AddWorkflowActionsFromAssemblyOf<WelcomeEmailWorkflow>();

// the dependencies of your workflow actions
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

builder.Services.AddHostedService<WorkflowWorker>();

builder.Build().Run();
```

The worker polls the store for workflows until the service stops:

```csharp
using Stateless.WorkflowEngine;

public class WorkflowWorker : BackgroundService
{
    private readonly IWorkflowServer _workflowServer;
    private readonly ILogger<WorkflowWorker> _logger;

    public WorkflowWorker(IWorkflowServer workflowServer, ILogger<WorkflowWorker> logger)
    {
        _workflowServer = workflowServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // the server logs completed, retried and suspended workflows itself (see Logging)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // run up to 100 workflows, 10 at a time - if there was nothing to do, wait before polling again
                int executed = await _workflowServer.ExecuteWorkflowsAsync(100, 10, stoppingToken);
                if (executed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // the service is stopping
            }
            catch (Exception ex)
            {
                // e.g. the database is unavailable - keep the service running and try again
                _logger.LogError(ex, "Error executing workflows");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
```

Add the connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Workflows": "mongodb://localhost:27017"
  }
}
```

Run it with `dotnet run` to test it as a console app.  To install it as a Windows service, publish it and create the service from an elevated PowerShell prompt:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o C:\Services\MyWorkflowServer
sc.exe create MyWorkflowServer binPath= "C:\Services\MyWorkflowServer\MyWorkflowServer.exe" start= delayed-auto
sc.exe failure MyWorkflowServer reset= 86400 actions= restart/60000/restart/60000/restart/60000
sc.exe start MyWorkflowServer
```

The `failure` command restarts the service if it crashes.  By default the service runs as LocalSystem - use `sc.exe config MyWorkflowServer obj= "DOMAIN\account" password= "..."` to run it as an account with access to your database and anything your actions use.  As a Windows service, `ILogger` warnings and errors are written to the Windows Event Log (Application log) - add a logging provider such as NLog or Serilog for log files.  To remove the service, run `sc.exe stop MyWorkflowServer` and `sc.exe delete MyWorkflowServer`.

Things to keep in mind:

* **You can run as many WorkflowServers against one store as you like** (e.g. several instances of the service, or containers) - each workflow is locked by the server executing it.  See [Running Multiple Servers](#running-multiple-servers).
* The `WorkflowServer` is a **singleton** (`AddStatelessWorkflowEngine` registers it as one) - its constructor initialises the store (creating tables and indexes if `WorkflowServerOptions` allows it).  When the service stops, `stoppingToken` is cancelled, which shuts the server's node down and releases its workflows straight away for the other servers (the host also disposes it, which does the same).
* Call `AddWorkflowType<T>()` for every workflow type.  This registers the type with the store (required for MongoDb), and saves the workflow's definition so the web console can show its graph.
* Each workflow action is created in its own DI scope, so actions can take scoped services (e.g. an Entity Framework `DbContext`) in their constructor - they are disposed once the action has executed.
* Make actions safe to retry: if an action fails part-way through, it runs again from the start.
* When workflow classes change, deploy the new version to both the server and the applications that register workflows.  Workflows already in the store are loaded into the new class, so avoid renaming or removing states and triggers that existing workflows may be in.

# Code and Concepts

## Creating a WorkflowServer

You will need a WorkflowServer, usually running in the context of a windows service, in order to process workflows in your application - see [Getting Started](#getting-started) for a complete example.

Note that this should be implemented in your application as a singleton, to prevent continuous schema checks which will slow down your application.

### Workflow Server Options

The `IWorkflowServer` class takes a `WorkflowServerOptions` parameter, where the following options can be set:

- `AutoCreateTables` - for workflow stores where tables must be explicitly created, if set to `true` this option will result in the server creating the tables if they do not exist.  Defaults to `true`.
- `AutoCreateIndexes` - if set to `true` this option will result in the server creating basic indexes (see below for more details).  Defaults to `true`.
- `PersistWorkflowDefinitions` - if set to true, workflow definitions will be inspected and stored if possible.  Defaults to `true`.
- `NodeId`, `NodeName`, `HeartbeatInterval`, `NodeTimeout` and `ClaimBatchSize` - control how the server claims and locks workflows and detects servers that have stopped.  See [Running Multiple Servers](#running-multiple-servers).

### Executing a Single Workflow

`IWorkflowServer` provides both synchronous and asynchronous methods for executing a single workflow instance directly:

| Method | Description |
|--------|-------------|
| `void ExecuteWorkflow(Workflow workflow)` | Synchronous. Fires the workflow's current trigger and saves state. |
| `Task ExecuteWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)` | Asynchronous equivalent. The cancellation token is forwarded through to the workflow's `FireAsync` call and into any `IWorkflowActionAsync` actions. |

You would typically use these methods only when you have a specific workflow instance already in hand (e.g. in tests or when reprocessing a suspended workflow). The batch methods below are the normal production path.  Note that these methods don't lock the workflow, so they shouldn't be used on workflows that a server may be executing.

### Executing Workflows in Bulk

`IWorkflowServer` provides batch execution methods that claim workflows from the store and execute them, with optional concurrency control:

| Method | Description |
|--------|-------------|
| `int ExecuteWorkflows(int count, int? maxConcurrent = null)` | Synchronous. Claims and executes up to `count` workflows, with no more than `maxConcurrent` running at once. |
| `Task<int> ExecuteWorkflowsAsync(int count, int? maxConcurrent = null, CancellationToken? cancellationToken = null)` | Asynchronous equivalent. Supports cancellation — already-dispatched workflows run to completion before the method returns. |

Workflows are **claimed in batches of up to `ClaimBatchSize`** (a `WorkflowServerOptions` setting, default 10): the execution slots take workflows from the claimed batch as they free up, and when the last one is taken the next batch is claimed in the background (one store call), so it is usually ready before the running workflows finish. A new batch is only claimed once the previous one has been taken, so a server holds locks on at most `ClaimBatchSize` workflows it hasn't started yet, and each claim picks the workflows that are first in line at that moment - ordered by **Priority DESC**, then **RetryCount DESC**, then **CreatedOn ASC** - including workflows registered while the batch is running.

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

#### Errors

Errors thrown by actions are handled by the workflow (retried, and eventually suspended) and aren't raised.  If a store call fails - claiming a batch, or saving or archiving a workflow - no more workflows are claimed, but every workflow already claimed is still executed, and then the first error is thrown.  A workflow whose save failed stays locked by the node until it is released as stale after `NodeTimeout` (see [Running Multiple Servers](#running-multiple-servers)), and is then executed again.

#### Cancellation

`ExecuteWorkflowsAsync` accepts a `CancellationToken`. Cancelling it stops new workflows from being dispatched but allows any already-running workflows to finish cleanly. The token is also forwarded into each `ExecuteWorkflowAsync` call, and from there into `workflow.FireAsync` and any `IWorkflowActionAsync` actions, so long-running async actions can observe it directly.

The token also **shuts the server's node down**.  The server keeps the most recent token passed to `ExecuteWorkflowsAsync`, and when it is cancelled - even between calls, e.g. while your loop waits to poll again - the server does what `Dispose` does (see [Running Multiple Servers](#running-multiple-servers)): it stops claiming, waits (up to `NodeTimeout`) for the workflows it is claiming or executing, then releases all of its locks and removes its node, so other servers can pick up its workflows straight away.  A server that has shut down doesn't claim any more workflows - create a new one to start again.  So pass your application's shutdown token (e.g. a `BackgroundService`'s `stoppingToken`), not a per-call timeout.

### Running Multiple Servers

Any number of `WorkflowServer`s - in different processes, on different machines or in containers - can execute workflows from the same store at the same time.  Each server is a **node**:

* **Claiming** - `ExecuteWorkflows` and `ExecuteWorkflowsAsync` claim a batch of workflows (`IWorkflowStore.ClaimNext(nodeId, count)`) in the background as soon as the previous batch has been taken by the execution slots.  Each workflow in a batch is claimed atomically - the store only stamps it with the node's id (`Workflow.LockedBy`) if it is still unlocked - so two nodes can never claim the same workflow.  MongoDB claims a batch in three round trips (find the candidates, `updateMany` the ones still available, read back the ones won); custom stores that derive from `WorkflowStore` get a default that claims one at a time.  The workflow stays locked while it executes - including any steps that run straight after each other - and the lock is cleared in the same write that saves the workflow after its last step.
* **Heartbeats** - each node registers itself in the `WorkflowNodes` collection when it first executes workflows, and a background task updates its heartbeat every `HeartbeatInterval`.  Locks belong to the node rather than having a time limit, so an action can run for as long as it needs while its node is alive.
* **Recovery** - on every heartbeat, each node removes the nodes that haven't heartbeated within `NodeTimeout`, and releases the workflows locked by nodes that no longer exist.  If a node crashes, its workflows are picked up by the other nodes (or by the node itself when it restarts) after `NodeTimeout`.  A node also releases its own locks on workflows it is no longer executing (e.g. a save failed because the database was briefly unavailable), once they are older than `NodeTimeout`.
* **Self-fencing** - a node that hasn't been able to heartbeat for half of `NodeTimeout` stops claiming workflows, well before the other nodes can consider it dead.  If it is removed anyway (e.g. after a long GC pause or a network partition), it raises `NodeEvicted` and re-registers with a new id.  A node's saves only succeed if the workflow hasn't been claimed by another node since - otherwise the server raises `WorkflowLockLost` and leaves the workflow to the node that now owns it.

The options that control this are set on `WorkflowServerOptions`:

| Option | Default | Description |
|--------|---------|-------------|
| `NodeId` | a new Guid per server instance | The id stored in `Workflow.LockedBy`.  If you set it, it must be unique across all servers using the store.  Available on `IWorkflowServer.NodeId`. |
| `NodeName` | the machine name | A display label only - it doesn't need to be unique.  In Kubernetes the machine name is the pod name, and in Docker it is the container id (unless `hostname` is set, or the container uses host networking, in which case containers share a name). |
| `HeartbeatInterval` | 10 seconds | How often the node heartbeats and checks for nodes that have stopped. |
| `NodeTimeout` | 60 seconds | How long a node can go without a heartbeat before the other nodes remove it and release its workflows.  Must be at least 3 x `HeartbeatInterval`.  Make it comfortably longer than any GC pause, network blip or database failover you expect - the trade-off is that the workflows of a node that crashes wait this long before another node picks them up. |
| `ClaimBatchSize` | 10 | The maximum number of workflows claimed in one store call.  Larger batches save a round trip per workflow (most noticeable with low `maxConcurrent`), but claimed workflows stay locked by the node until a slot is free to run them, so other nodes can't pick them up meanwhile.  Set it to 1 to claim one workflow at a time. |

Things to keep in mind:

* **Dispose the server, or cancel its token, when your application stops.**  It stops claiming workflows, waits (up to `NodeTimeout`) for the workflows it is claiming or executing to finish, then releases its locks - including any held under the ids it had before it was [evicted](#running-multiple-servers) - and removes its node, so the other nodes don't have to wait for the timeout.  A server registered as a singleton in a host's service provider is disposed automatically, and cancelling the token last passed to `ExecuteWorkflowsAsync` does the same as soon as the application starts stopping (see [Cancellation](#cancellation)).
* **Make actions idempotent.**  The engine guarantees that a step never runs on two nodes at the same time - not that it runs exactly once.  If a node crashes after an action finishes but before the workflow is saved, the step runs again on another node.  (This was already true for a single server that crashes and restarts.)
* **Upgrade every server together.**  Servers older than 6.0 don't lock workflows, so stop all of them before starting 6.0 servers on the same store.  Existing workflows don't need migrating - workflows without lock fields are treated as unlocked.
* **RavenDb clusters** - see the [RavenDb caveats](#ravendb-caveats) below.
* `ExecuteWorkflow`/`ExecuteWorkflowAsync` (a single workflow) don't claim or lock the workflow - use the bulk methods to process workflows.

#### RavenDb caveats

* RavenDB has no atomic find-and-modify, so a RavenDb claim reads candidate workflows from an index and claims one with a conditional patch, which RavenDB applies atomically, and saves use optimistic concurrency.  These guarantees only hold when **all workflow servers write to the same RavenDB server**.  In a multi-server RavenDB cluster, keep the client's default `ReadBalanceBehavior.None` and don't spread writes across cluster nodes with a load balance behavior: if two workflow servers claim the same workflow on different database servers, both will run it, and RavenDB only resolves the conflict afterwards.  The [stress test](#multi-node-stress-test) runs against a single RavenDB server, so it can't detect this.
* RavenDb node heartbeats are compared using the clocks of the workflow servers (MongoDb uses the database server's clock), so keep server clocks in sync (e.g. with NTP), and keep `NodeTimeout` much larger than any clock difference.

## Creating a WorkflowClient

A WorkflowClient is used to register workflows for processing by the WorkflowServer - see [Getting Started](#getting-started).  It can also check on and manage workflows that have been registered (each method has an async equivalent):

```csharp
workflowClient.Register(workflow);                                // save a new workflow for the server to run
bool exists = workflowClient.Exists(workflowId);                  // false once the workflow has completed
MyWorkflow wf = workflowClient.Get<MyWorkflow>(workflowId);       // load an incomplete workflow
workflowClient.Unsuspend(workflowId);                             // retry a suspended workflow
workflowClient.Delete(workflowId);                                // remove an incomplete workflow
long suspended = workflowClient.GetSuspendedCount();              // also GetIncompleteCount() and GetCompletedCount()
```

Like the server, the client should be created once and reused - `AddStatelessWorkflowEngine` registers it as a singleton (see [Dependency Injection](#dependency-injection)).

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

With `Microsoft.Extensions.DependencyInjection`, `AddStatelessWorkflowEngine` registers the workflow server (`IWorkflowServer`) and client (`IWorkflowClient`) as singletons, and returns a builder to add a store, workflow types and workflow actions (see [Getting Started](#getting-started) for a complete example):

```csharp
services.AddStatelessWorkflowEngine(options => options.ClaimBatchSize = 20)
    .WithMongoDbStore(connectionString, "MyApp")
    .AddWorkflowType<WelcomeEmailWorkflow>()
    .AddWorkflowActionsFromAssemblyOf<WelcomeEmailWorkflow>();
```

| Method | Description |
|--------|-------------|
| `AddStatelessWorkflowEngine(configure)` | Adds the server and client.  `configure` sets the `WorkflowServerOptions` - they can also be bound to configuration with `services.Configure<WorkflowServerOptions>(configuration.GetSection("Workflows"))`. |
| `WithMongoDbStore(connectionString, databaseName, configure)` | Stores workflows in MongoDb (`Stateless.WorkflowEngine.MongoDb` package).  `configure` can change the collection names.  To share a client you have registered, pass a factory instead: `WithMongoDbStore(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("MyApp"))`. |
| `WithRavenDbStore(urls, database, certificate)` | Stores workflows in RavenDb (`Stateless.WorkflowEngine.RavenDb` package).  To share a document store you have registered, pass a factory instead: `WithRavenDbStore(sp => sp.GetRequiredService<IDocumentStore>())`. |
| `WithMemoryStore()` | Stores workflows in memory - they are lost when the application stops. |
| `WithStore<TStore>()` / `WithStore(sp => ...)` | Uses your own `IWorkflowStore`. |
| `AddWorkflowType<T>()` | Registers a workflow type with the server when it is created (see `RegisterWorkflowType` below).  Add every workflow type the server executes. |
| `AddWorkflowActionsFromAssemblyOf<T>(filter)` | Registers every `IWorkflowAction` and `IWorkflowActionAsync` in the assembly as transient (unless already registered).  `filter` limits it to some of the types, e.g. one namespace. |

The store is a singleton shared by the server and client.  The server, and any `MongoClient` or `DocumentStore` created from a connection string or urls, are disposed with the service provider - disposing the server releases its workflows for other servers (see [Running Multiple Servers](#running-multiple-servers)).  An application that only registers workflows can use the same setup and just resolve `IWorkflowClient` - the server is only created when it is resolved.

### Workflow actions

The server creates workflow actions from the service provider (`AddStatelessWorkflowEngine` sets `IWorkflowServer.ServiceProvider`):

* **Each action runs in its own scope**, which is disposed once the action has executed, so an action can take scoped services (e.g. an Entity Framework `DbContext`) in its constructor and gets new ones every time it runs.
* **Actions don't have to be registered** - an action that isn't registered is created with its constructor dependencies resolved from the scope.  Registering them (`AddWorkflowActionsFromAssemblyOf<T>()`, or `services.AddTransient<MyAction>()`) means the service provider's `ValidateOnBuild` (on by default in the Development environment) checks their dependencies when the application starts.  Otherwise, an action with a missing dependency throws when it runs, and the workflow is retried and eventually suspended.

### Other containers, or no container

If you create the `WorkflowServer` yourself, set its `ServiceProvider` property to have actions created the same way.  Containers such as Autofac, Lamar and DryIoc provide an `IServiceProvider` through their `Microsoft.Extensions.DependencyInjection` integration, so they can be used here too:

```csharp
workflowServer.ServiceProvider = serviceProvider;
```

Without a service provider, actions are created with their parameterless constructor.  To create actions yourself, override `CreateWorkflowActionInstance<T>(IServiceProvider)` (and `CreateWorkflowActionAsyncInstance<T>(IServiceProvider)`) on your workflow - it is given the scope the action executes in, or null if there is no service provider.

**Upgrading from 5.x:** `IWorkflowEngineDependencyResolver` and the `DependencyResolver` property have been removed.  Use `AddStatelessWorkflowEngine`, or set `ServiceProvider` to your container's `IServiceProvider` - your resolver's `GetInstance<T>()` is replaced by the container resolving (or creating) each action in its own scope.  `CreateWorkflowActionInstance<T>()` and `CreateWorkflowActionAsyncInstance<T>()` now take the action scope (`IServiceProvider`), and are called whether or not there is a service provider.

## Logging

The server logs through `ILogger` - `AddStatelessWorkflowEngine` uses the application's `ILoggerFactory`, or pass one to the `WorkflowServer(store, options, loggerFactory)` constructor.  There are two categories:

| Category | Level | Event id | Message |
|----------|-------|----------|---------|
| `Stateless.WorkflowEngine.WorkflowServer` | Debug | 1000 | A batch of workflows was claimed |
| | Debug | 1001 | A workflow step is executing (state and trigger) |
| | Information | 1002 | A workflow completed |
| | Warning | 1003 | A workflow action failed, and the workflow will be retried (with the exception, and when it will retry) |
| | Error | 1004 | A workflow action failed, and the workflow has been suspended |
| | Warning | 1005 | A workflow's lock was lost to another node, so its result wasn't saved |
| | Warning | 1006 | A `WorkflowClaimed` event handler threw |
| | Error | 1007 | Claiming workflows failed (e.g. the database is unavailable) |
| | Error | 1008 | A claimed workflow couldn't be executed or saved |
| `Stateless.WorkflowEngine.Services.WorkflowNodeMonitor` | Information | 1100 | The node registered |
| | Debug | 1101 | The node heartbeated |
| | Warning | 1102 | The node was removed by another node, and re-registered with a new id |
| | Information | 1103 | Nodes that stopped heartbeating were removed |
| | Information | 1104 | Workflows locked by a node that no longer exists were released |
| | Error | 1105 | A heartbeat or monitoring operation failed |
| | Warning | 1106 | A `NodeMonitorError` event handler threw |
| | Information | 1107 | The node is stopping |
| | Warning | 1108 | The node stopped waiting for its running workflows after `NodeTimeout` |
| | Information | 1109 | The node stopped, released its locks and removed itself |

With a high volume of workflows, Information logs a line for every completed workflow - set the `Stateless.WorkflowEngine.WorkflowServer` category to `Warning` to only log problems, e.g. in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Stateless.WorkflowEngine.WorkflowServer": "Warning"
    }
  }
}
```

The server's events (see below) are still raised, for anything you want to do beyond logging.

## Events

Workflows move through states, but there are also events that occur in the lifecycle that are raised, allowing you to take 
action.

### WorkflowServer

The `IWorkflowServer` interface exposes the following events:

1. `WorkflowSuspended` - this event is raised by the WorkflowServer when a workflow suspends.  This means that a workflow has errored repeatedly until the maximum number of retries (RetryCount) defined for the workflow has been exceeded.  The workflow goes into a suspended state and will not be picked up again by the WorkflowEngine until the problems have been resolved.  
2. `WorkflowCompleted` - this event is raised when a workflow completes.  The event is fired AFTER the workflow is archived and the workflow.OnComplete() method is called.  
3. `WorkflowClaimed` - raised when the server claims a workflow, just before executing it (e.g. to log or count the workflows each server picks up).  Exceptions thrown by handlers are ignored, as the claimed workflow must still be executed.
4. `WorkflowLockLost` - raised when the server can't save a workflow it executed, because another server has claimed it since (or it was deleted).  The workflow isn't saved and the server stops executing it.  This only happens if the server was considered dead by the other servers - see [Running Multiple Servers](#running-multiple-servers) - so log it as an error.
5. `NodeEvicted` - raised when the server finds it was removed by the other servers (it couldn't heartbeat within `NodeTimeout`) and has re-registered with a new node id.
6. `NodeMonitorError` - raised when the server's background heartbeat fails (e.g. the database is unavailable).  The server keeps running, but stops claiming workflows if it can't heartbeat for half of `NodeTimeout`.

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

### MongoDb

The following indexes are created by default:

* Workflows: Priority descending, RetryCount descending, CreatedOn ascending
* Workflows: IsSuspended - a partial index containing only suspended workflows, used for suspended and active counts
* Workflows: LockedBy - a partial index containing only the workflows currently locked by a server, used by the lock checks every server runs on each heartbeat
* CompletedWorkflows: CreatedOn descending

The `WorkflowNodes` collection is also created.  Workflows only store the `LockedBy` and `LockedOn` fields while they are locked, which keeps the LockedBy index small.  Node heartbeats are set and compared using the database server's clock, so they aren't affected by clock differences between the workflow servers.

The active workflow count (`GetActiveCount`) is calculated as the estimated size of the Workflows collection less the suspended count, rather than by scanning the collection.  Like the incomplete and completed counts, it is an estimate - it can be slightly out after an unclean shutdown or when orphaned documents exist in a sharded cluster.

### RavenDb

No indexes are created - `Initialise()` only creates the database.  The store uses dynamic queries, which RavenDB always answers from **auto indexes**: it creates one for each query shape the first time it runs (e.g. `Auto/RavenWorkflows/ByWorkflow.IsSuspended`), and keeps it up to date from then on.  Note that on an existing large database, results from a newly created auto index can be incomplete until RavenDB has finished building it.

Versions prior to 6.0 deployed two static indexes, `WorkflowIndex/Priority/RetryCount/CreatedOn` and `CompletedWorkflowIndex/CreatedOn`.  No query ever used them, so they only added indexing and storage overhead.  They are no longer created, and existing copies can safely be deleted from the database in the RavenDB Studio (Indexes > List of Indexes).

Running servers are stored in the `WorkflowNodes` collection.  When running more than one workflow server against a RavenDB cluster, read the [RavenDb caveats](#ravendb-caveats) - claims are only atomic when every workflow server writes to the same RavenDB server.

## Manually creating indexes: MongoDb

On the CompletedWorkflows collection:

```csharp
db.CompletedWorkflows.createIndex({ "Workflow.CreatedOn" : -1 }, { name : "CompletedWorkflow_CreatedOn" })
```

On the Workflows collection (only create this if you generate a lot of workflows - usually your active workflow count should stay low so an index is not necessary):

```csharp
db.Workflows.createIndex({ "Workflow.Priority" : -1, "Workflow.RetryCount" : -1, "Workflow.CreatedOn" : 1 }, { name : "Workflow_Priority_RetryCount_CreatedOn" })
```

On the Workflows collection, a small partial index of suspended workflows (recommended - it keeps suspended and active counts fast, which the web console requests on every dashboard load):

```csharp
db.Workflows.createIndex({ "Workflow.IsSuspended" : 1 }, { name : "Workflow_IsSuspended", partialFilterExpression : { "Workflow.IsSuspended" : true } })
```

On the Workflows collection, a small partial index of locked workflows (recommended - every server queries locks on each heartbeat):

```csharp
db.Workflows.createIndex({ "Workflow.LockedBy" : 1 }, { name : "Workflow_LockedBy", partialFilterExpression : { "Workflow.LockedBy" : { $exists : true } } })
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

#### Guids

Version 3 of the MongoDb driver (used from version 6.0 of the store) requires the Guid format to be configured.  Earlier versions of the store saved Guids in the C# legacy format (binary subtype 3), so the store configures workflow and workflow definition ids to use that format - existing workflows can be read without any changes.

**If your workflow classes have their own `Guid` properties**, register a Guid serializer at application start-up, *before* creating a `MongoClient` or using the store:

```csharp
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));
```

Without this, the driver will throw an exception when saving or loading those workflows (`GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified`).  Use `CSharpLegacy` to keep reading workflows saved by earlier versions - only use another representation (such as `Standard`) for a new store with no existing data, as Guids saved in different formats do not match each other in queries.

# Workflow Definitions


The `IWorkflowServer` will attempt to persist workflow definitions into a `WorkflowDefinitions` collection or table when types are registered.  These will result in graphs that can be rendered in the [web console](#web-console) displaying a representation of the workflow.  This option can be turned off using the `WorkflowOptions` property.

# Web Console

The web console is a browser-based tool for monitoring and managing workflow stores.  It is an ASP.NET Core (.NET 10) application that serves a Vue / Nuxt UI single page app, and runs as a Windows service or a Docker container.  It supports **MongoDb** (MongoDb Server 4.4 or later) and **RavenDb** stores.

Features:

* Dashboards of connections to workflow stores, showing active, suspended and completed workflow counts (with optional auto refresh)
* A list of incomplete workflows per store - view the workflow JSON, view the workflow definition graph, and suspend, unsuspend, mark as completed or delete workflows individually or in bulk
* User management with Admin and User roles - Users can be granted individual permissions
* A notification when a new version of the console is available

See [Installing as a Windows service](#installing-as-a-windows-service) or [Running in Docker](#running-in-docker) to get started, then sign in with **admin / admin** and change the password (user menu, bottom left > **Change password**).

## Dashboards

![Dashboard](docs/images/webconsole/dashboard.png)

The dashboard shows a card for each connection, with its active, suspended and completed workflow counts.  Suspended workflows are highlighted in red, and a connection that can't be reached shows the error instead of its counts.  Switch to **Table** for a compact view of many connections, and turn on **Auto refresh** to keep the counts up to date.  Click a connection to see its workflows.

Connections are grouped into dashboards (e.g. one per environment or team), listed on the left - use **+** to add a dashboard, and the settings icon next to its name to rename or delete it.  There is always a **Default** dashboard, which can be renamed but not deleted.  A connection's menu (**⋮**) has options to edit it, delete it, or copy it to another dashboard.

## Connections

Click **Add connection** to connect to a workflow store, and **Test connection** to check the settings before saving them.  Passwords are encrypted (see [Configuration](#configuration)), and are never sent back to the browser - leave the password blank when editing a connection to keep the existing one.

**MongoDb** connections need the host, port (default 27017), database and the names of the active and completed workflow collections (`Workflows` and `CompletedWorkflows` unless your application changed them).  Optionally set a user and password, a replica set, and the workflow definition collection (default `WorkflowDefinitions`).

**RavenDb** connections need the host, port (default 8080) and database - the collections are fixed by the store, so they are not configured.

![RavenDb connection](docs/images/webconsole/connection-ravendb.png)

To connect to a secured RavenDb server, which uses https and authenticates with a client certificate, set **Client certificate** to the path of the certificate (`.pfx`) file on the console's server, and **Certificate password** to its password (if any).  In Docker, put the certificate in the `/app/Data` volume, e.g. `/app/Data/certs/client.pfx`.

The console doesn't have your workflow types loaded, so it works with the raw workflow documents.  Versions of the RavenDb store prior to 6.0 loaded the workflow type to suspend, unsuspend and delete a workflow - from 6.0 these (and marking a workflow as completed) patch, move or delete the document directly, as the MongoDb store does.

## Workflows

![Workflows](docs/images/webconsole/workflows.png)

The workflow list shows the incomplete workflows in a store - completed workflows are not shown.  Suspended workflows are highlighted, and can be sorted to the top with the **Suspended** column.

* **Show** sets how many workflows are loaded (up to 1000) - sorting and filtering apply to the loaded rows only.  To find a specific workflow in a large store, paste its full id into the filter and it will be looked up directly.
* Select workflows with the checkboxes to **Suspend**, **Unsuspend**, **Mark as completed** or **Delete** them in bulk (each asks for confirmation first).  Unsuspending a workflow resets its retry count, so it is picked up by the server straight away.  Marking a workflow as completed moves it into the completed workflows as it is, without running any further steps or workflow events - use it for workflows that no longer need to run but should be kept rather than deleted.
* Click a workflow's row to see its full JSON document, which can be copied, and to suspend, unsuspend, mark as completed or delete it.

![Workflow JSON](docs/images/webconsole/workflow-detail.png)

* Click a workflow's type (or use **Definitions**) to see its state machine graph.  Graphs are only available for workflows whose definitions are saved to the store - see [Workflow Definitions](#workflow-definitions).

![Workflow definition](docs/images/webconsole/definition-graph.png)

## Users and permissions

![Users](docs/images/webconsole/users.png)

Admins can do everything, including managing users.  Users can view all dashboards and workflows, and can be granted individual permissions to:

* add, edit and delete connections
* add, rename and delete dashboards
* suspend, unsuspend, mark as completed and delete workflows
* view, add, edit and delete users

Actions a user doesn't have permission for are hidden.  Every user can change their own password from the user menu.

## Installing as a Windows service

1. Install the [ASP.NET Core 10 runtime (Hosting Bundle)](https://dotnet.microsoft.com/download/dotnet/10.0) on the server
2. Download `Stateless.WorkflowEngine.WebConsole_v{version}.zip` from the [latest release](https://github.com/mrsalmon1976/Stateless.WorkflowEngine/releases/latest) and unzip it into the install folder
3. In an elevated PowerShell prompt in the install folder, run `.\install-service.ps1` (use `.\install-service.ps1 -Uninstall` to remove the service)
4. Browse to `http://localhost:8811` and sign in with **admin / admin** - change this password immediately

## Configuration

Settings are in `appsettings.json`.  To override them, create an `appsettings.Production.json` in the install folder - this file is preserved when upgrading.

| Setting | Default | Description |
|---------|---------|-------------|
| `Urls` | `http://localhost:8811` | The url(s) the console listens on.  Use e.g. `http://*:8811` to allow access from other machines |
| `DataPath` | `Data` | Folder for users, connections and encryption keys (relative to the install folder) |
| `LatestVersionUrl` | GitHub releases API | Used to check for new versions |
| `UpdateCheckIntervalInMinutes` | `60` | How long the version check is cached for |

Users and connections are stored in `Data\users.json`.  Connection passwords are encrypted with ASP.NET Core Data Protection, with the keys stored in `Data\keys` - back up the whole `Data` folder, as connection passwords can't be decrypted without the keys.

## Upgrading

The console shows a banner when a new version is available, but upgrades are run manually: in an elevated PowerShell prompt in the install folder, run `.\Update.ps1`.  This downloads the latest release, backs up the current install to `Backup\{version}`, and replaces the application files, keeping `Data`, `logs` and `appsettings.Production.json`.

**Upgrading from version 5.x or earlier** (the .NET Framework console):

* Install the ASP.NET Core 10 runtime first
* Back up `Data\users.json`
* Copy `Update.ps1` from the new release zip over the old one before running it - the old script can't install the new service
* Existing users and connections are kept.  Saved connection passwords are re-encrypted on first start, after which the old console can no longer read them

## Running in Docker

```bash
docker build -f source/Stateless.WorkflowEngine.WebConsole/Dockerfile -t stateless-workflowengine-webconsole source
docker run -d -p 8811:8080 -v webconsole_data:/app/Data stateless-workflowengine-webconsole
```

The `/app/Data` volume holds users, connections and the encryption keys, so it must be persisted.  Settings can be overridden with environment variables, e.g. `-e LatestVersionUrl=...`.  Note that inside a container, `localhost` refers to the container itself - connect to MongoDb or RavenDb using a host name reachable from the container.

## Performance on large stores

Each dashboard load (and auto refresh) requests the workflow counts for every connection.  On MongoDb these use collection metadata and the partial `Workflow_IsSuspended` index (see [Default Indexes](#default-indexes)), so make sure that index exists on large stores.  On RavenDb they use auto indexes, which RavenDB creates on the first request (see [Default Indexes](#default-indexes)) - on a large existing database the counts can be incomplete until it has finished building them.

# Troubleshooting

## A long-running or hung action delays other workflows

The engine has no action timeout.  `IWorkflowAction.Execute` can't be interrupted, and `IWorkflowActionAsync.ExecuteAsync` only stops if it observes the cancellation token passed to `ExecuteWorkflowsAsync`.  An action that throws is handled normally (the workflow is retried and eventually suspended), but an action that never returns keeps its execution slot - and its workflow's lock - for as long as it runs.  This is deliberate: releasing the lock while the action is still running would let another node run the same step at the same time.

Because workflows are [claimed in batches](#executing-workflows-in-bulk), the workflows claimed alongside a hung action also wait: they stay locked by the node until a slot is free to run them, and other nodes can't pick them up.  With `maxConcurrent` of 1, the whole batch waits, and so does the `ExecuteWorkflows` call.  Slow actions have the same effect on a smaller scale.

To avoid this:

* Make sure actions can't block forever - set timeouts on all I/O (database commands, HTTP calls, locks), and avoid blocking on async code (`.Result`/`.Wait()`), which can deadlock.
* In async actions, pass the cancellation token through to the calls you make.
* Use a `maxConcurrent` greater than 1, so other slots keep running the rest of the batch.
* Lower `ClaimBatchSize` (down to 1) if your actions are slow, so fewer workflows are held by a node waiting for a slot.

When a server with a hung action is disposed, it waits up to `NodeTimeout` and then releases its locks, so its workflows - including the hung one - become available to other nodes even though the action may still be running.

## A workflow that can't be saved is retried forever

If a workflow's action succeeds but the workflow can't be saved - for example a custom property that can't be serialised, or a document over the store's size limit (16MB on MongoDB) - the failure isn't a workflow error, so the workflow's `RetryCount` isn't increased and it is never suspended.  Instead:

1. `ExecuteWorkflows`/`ExecuteWorkflowsAsync` finishes the other workflows it has claimed, then throws the store's exception.
2. The workflow stays locked by the node until the node's stale lock release frees it, after `NodeTimeout`.
3. It is then claimed again, its action runs again, the save fails again - and so on, every `NodeTimeout`.

Nothing is lost, but the action keeps re-running (another reason to [make actions idempotent](#running-multiple-servers)).  The sign is the same exception thrown by `ExecuteWorkflows` at regular intervals - log exceptions from your execution loop rather than swallowing them.  To fix it, correct the workflow's data or delete it (e.g. with `WorkflowClient.Delete` or the [web console](#web-console)), and keep custom workflow properties small and serialisable.

# Development

`start.ps1` in the repository root starts a local environment in Docker (starting Docker Desktop if necessary):

* MongoDb on `mongodb://localhost:27017`
* RavenDb on `http://localhost:8080` (an unsecured development instance)
* The web console on `http://localhost:8811`, built from source, with connections to the example MongoDb and RavenDb databases

```powershell
.\start.ps1            # start everything, rebuilding the console image
.\start.ps1 -NoBuild   # start without rebuilding the console image
.\start.ps1 -Stop      # stop the containers (data volumes are kept)
```

The example application and the MongoDb/RavenDb test projects use these containers by default.  Tests that need a database are marked with the `RequiresMongoDb` and `RequiresRavenDb` categories.

Every project targets .NET 10, so the solution builds and tests with the .NET 10 SDK alone:

```powershell
dotnet build source\Stateless.WorkflowEngine.sln
dotnet test source\Stateless.WorkflowEngine.sln     # needs start.ps1 running for the database tests
dotnet test source\Stateless.WorkflowEngine.sln --filter "TestCategory!=RequiresMongoDb&TestCategory!=RequiresRavenDb"   # no databases needed
```

To work on the web console with hot reload, run the API and the Vite dev server (which proxies `/api` calls to the API):

```powershell
dotnet run --project source\Stateless.WorkflowEngine.WebConsole        # http://localhost:8811
cd source\Stateless.WorkflowEngine.WebConsole.Client; npm install; npm run dev   # http://localhost:5173
```

Building a release (`deployment\build.ps1`) requires the .NET 10 SDK and Node.js, as publishing the console builds the client app.

## Multi-node stress test

`test-multinode.ps1` in the repository root proves that several workflow servers running against the same store never execute the same workflow step at the same time, and that no workflow is left behind when a server crashes.  For each store (MongoDb, then RavenDb - pick one with `-Store mongo` or `-Store raven`) it seeds 2000 workflows, starts 3 servers (alternating sync and async) and a fourth server that is killed part way through, each in its own window (`-Hidden` to hide them), restarts the killed server, waits for everything to finish and runs a report:

```powershell
.\start.ps1                                     # MongoDb and RavenDb must be running
.\test-multinode.ps1                            # both stores
.\test-multinode.ps1 -Store mongo -Count 500 -Nodes 5
```

Each workflow has three steps that write and read files, sleep for a random time, occasionally run for longer than the node timeout (to prove long actions keep their lock), and throw a small number of random exceptions (to exercise retries).  Every step execution is recorded in an execution ledger in MongoDb (database `StatelessMultiNodeLedger_mongo`/`_raven`), which is independent of the store being tested.  The report fails if:

* any step ran on two servers at the same time (checked from the ledger, and live by the servers themselves)
* any step succeeded twice, other than the expected re-run of a step whose server was killed after the step finished but before the workflow was saved
* any workflow didn't complete, was suspended or is still locked, or any step never finished on a server that wasn't killed

Output from a run (the numbers vary from run to run - ExpectedReexecutions is only non-zero when the killed server had finished a step but not yet saved the workflow):

```
=== Summary ===
Store Result Seeded Completed Active Suspended LockedByNodes StepExecutions Overlaps DuplicateSuccesses Unfinished ExpectedReexecutions
----- ------ ------ --------- ------ --------- ------------- -------------- -------- ------------------ ---------- --------------------
mongo PASS     2000      2000      0         0             0           6122        0                  0          0                    0
raven PASS     2000      2000      0         0             0           6131        0                  0          0                    0
```

The individual commands can also be run by hand, e.g. to run nodes on several machines or in containers against the same store (the options are listed in `source/Example/MultiNode/MultiNodeExample.cs`):

Build once, then run each command from the built DLL - don't use `dotnet run` for nodes, as it rebuilds the project every time, which fails while another node (or a debug session) has the DLLs open.  The Release build also keeps the nodes clear of the Debug output that Visual Studio uses:

```powershell
dotnet build source\Example -c Release
$example = "source\Example\bin\Release\net10.0\Example.dll"

dotnet $example multinode reset  --store mongo
dotnet $example multinode seed   --store mongo --count 2000
dotnet $example multinode node   --store mongo --name A --concurrency 10              # one per window
dotnet $example multinode node   --store mongo --name B --concurrency 10 --async
dotnet $example multinode node   --store mongo --name C --crash-after 20              # killed after 20 seconds
dotnet $example multinode report --store mongo
```

Each node exits once the store is empty (it keeps polling while workflows are locked by a killed node, until they are released after the node timeout).  Other node options: `--batch 200`, `--node-timeout 20`, `--error-rate 0.02`, `--io-kb 256`, `--slow-rate 0.005` and `--max-minutes 30`.

Node logs and the report summaries are written to the `logs` folder.
