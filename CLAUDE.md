# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Stateless.WorkflowEngine is a .NET workflow engine built as a wrapper around the [Stateless](https://github.com/nblumhardt/stateless) state machine library. It adds persistence, retry mechanisms, exception handling, and workflow suspension on top of Stateless.

## Build Commands

```bash
# Build the solution
dotnet build source/Stateless.WorkflowEngine.sln

# Build via deployment script (packages releases)
powershell -File deployment/build.ps1

# Publish NuGet packages
powershell -File deployment/publish.ps1
```

## Testing

- **Framework**: NUnit 4.6.1 with NSubstitute 5.3.0 for mocking
- **Test projects** are SDK-style net10.0. `Test.Stateless.WorkflowEngine.MongoDb`/`.RavenDb` reference `Test.Stateless.WorkflowEngine` for the shared `WorkflowStoreTestBase`; `Test.Stateless.WorkflowEngine.WebConsole` includes `WebApplicationFactory` integration tests

`start.ps1` (repo root) starts MongoDB (27017), RavenDB (8080) and the web console (8811) in Docker via `docker-compose.yml`. The console container is a baked image, so it only reflects source changes after a rebuild; `start.ps1 -Dev` instead stops that container and runs the console from source with live reload (`dotnet watch` on 8811 + Vite on 5173). The Example app and the `RequiresMongoDb`/`RequiresRavenDb` tests use these containers by default (override with `STATELESS_MONGODB_CONNECTIONSTRING` / `STATELESS_RAVENDB_URL`).

```bash
# Run all tests (from solution root)
dotnet test source/Stateless.WorkflowEngine.sln

# Run a specific test project
dotnet test source/Test.Stateless.WorkflowEngine/Test.Stateless.WorkflowEngine.csproj
```

Test projects:
- `Test.Stateless.WorkflowEngine` — core engine tests
- `Test.Stateless.WorkflowEngine.MongoDb` — MongoDB store tests
- `Test.Stateless.WorkflowEngine.RavenDb` — RavenDB store tests
- `Test.Stateless.WorkflowEngine.WebConsole` — web console tests

## Architecture

### Core Components

- **`Workflow`** (`Stateless.WorkflowEngine/Workflow.cs`) — abstract base class. Custom workflows override `Initialise(initialState)` to configure Stateless states/triggers. Custom properties are serialized and persist across executions.
- **`WorkflowServer`** — processes queued workflows. The `maxConcurrent` slots share batches claimed with `IWorkflowStore.ClaimNext(nodeId, count)` (up to `WorkflowServerOptions.ClaimBatchSize`, default 10, per claim; the next batch is claimed in the background as soon as the slots have taken the previous one, one claim in flight at a time; Priority DESC, RetryCount DESC, CreatedOn ASC). Supports concurrent execution via `maxConcurrent` parameter. Should be a singleton, and is `IDisposable` (dispose releases its locks). Multiple servers (nodes) can share a store — see "Multi-node locking".
- **`WorkflowClient`** — registers workflows for processing by the server.
- **`IWorkflowStore`** — persistence abstraction. Collections: `Workflows` (active), `CompletedWorkflows` (archived) and `WorkflowNodes` (running servers). Every store method has a sync and an async version (except `RegisterType`, which does no I/O).
- **`IWorkflowAction`** — interface for actions executed at each workflow step.

### Store Implementations

| Store | Project | Target | Key Dependency |
|-------|---------|--------|---------------|
| MemoryWorkflowStore | Core library | net10.0 | — |
| MongoDbWorkflowStore | `Stateless.WorkflowEngine.MongoDb` | net10.0 | MongoDB.Driver 3.13.0 (server 4.4+) |
| RavenDbWorkflowStore | `Stateless.WorkflowEngine.RavenDb` | net10.0 | RavenDB.Client 7.2.7 |

### Workflow Lifecycle

1. Custom workflow registered via `WorkflowClient.Register()`
2. `WorkflowServer.ExecuteWorkflows*` claims it atomically (`ClaimNext` sets `Workflow.LockedBy` = node id) and executes it
3. Gets next action, fires Stateless trigger, action executes
4. Saved with `SaveClaimed` (only succeeds if not locked by another node); the lock is cleared in that same save unless the next step runs immediately or the workflow completes
5. On completion → archived to `CompletedWorkflows` (`ArchiveClaimed`)
6. On error → retry with configurable intervals (default: 5, 10, 15, 30, 60 seconds)
7. After max retries → workflow suspends

### Multi-node locking

- Locks are per node, not time-limited. `Services/WorkflowNodeMonitor` registers the node (`WorkflowNodes` collection) before the first claim, then every `HeartbeatInterval`: heartbeats, removes nodes stale for `NodeTimeout`, releases locks of owners with no node (owners MUST be read before nodes), and releases the node's own locks older than `NodeTimeout` that aren't in flight. `CanClaim` is false once the last heartbeat is older than `NodeTimeout / 2` (self-fencing). A removed node re-registers with a new id (`NodeEvicted`).
- Every claimed workflow is tracked as in flight (counted, so a re-claim after eviction isn't untracked early) and must be untracked, or the node never releases its lock. A claim or store error stops further claiming, but the slots still execute (and untrack) every workflow already claimed before `ExecuteWorkflows*` rethrows the first error unwrapped; a lock left behind by a failed save is then released by the node's stale lock release.
- Shutdown (`WorkflowNodeMonitor.StopAsync`) runs on `Dispose` or when the token last passed to `ExecuteWorkflowsAsync` is cancelled (`SetShutdownToken`): stops claiming, waits up to `NodeTimeout` for claims in progress (`TryBeginClaim`/`EndClaim`) and in-flight workflows, then releases locks under the current and any pre-eviction node ids and removes the node. A stopped node never claims again.
- Public `ExecuteWorkflow(workflow)` does not claim/lock (unconditional `Save`/`Archive`); only the bulk methods use the claimed path.
- Store specifics: Mongo claims one with `FindOneAndUpdate` and a batch with find → `updateMany` → find-back (matched on a process-unique `LockedOn` claim time), doesn't store null lock fields (`SetIgnoreIfNull`, partial `Workflow_LockedBy` index on `$exists`), and uses server time for heartbeats (`$currentDate`/`$$NOW`). Raven queries candidates in RQL (documents saved before v6 have no `LockedBy` field, which Raven doesn't treat as null), claims with a conditional patch, saves with optimistic concurrency, and is only atomic against a single Raven server. Memory tracks locks in a dictionary separate from the shared workflow objects.
- `test-multinode.ps1` (repo root) runs the Example `multinode` stress test against both stores; the execution ledger is in MongoDB.

### Benchmarks

`benchmarks/run-benchmarks.ps1` compares every `benchmarks/releases/Stateless.WorkflowEngine.MongoDb_v<version>.zip` with the current branch (1000 two-step workflows; nodes×concurrency 1×1, 1×4, 2×2, 1×10, 2×10, 5×10; 3 iterations) and writes `benchmarks/reports/benchmark-YYYYMMDD-HHMM.md`. `Benchmark.Runner` generates and builds one `Benchmark.Agent` per version under `benchmarks/.work` (releases are netstandard2.0 on their own MongoDB driver, so each needs its own process), with `LEGACY_EXECUTE` (<5.0) and `MULTINODE` (current/≥6.0) compile constants. Nodes run in `dotnet/runtime` containers on the MongoDB container's network by default — Docker Desktop stalls host-to-container requests over ~750 bytes by 40 ms, which skews results (`--host local` to run on the host).

### DI and logging

- `AddStatelessWorkflowEngine()` (core, `DependencyInjection/`, namespace `Microsoft.Extensions.DependencyInjection`) registers `IWorkflowServer`/`IWorkflowClient` singletons and returns an `IWorkflowEngineBuilder`; stores are added with `WithMemoryStore`/`WithStore` (core), `WithMongoDbStore` (MongoDb package) and `WithRavenDbStore` (RavenDb package) — each `With*Store` replaces the previous one. `AddWorkflowType<T>` runs `RegisterWorkflowType<T>` when the server is created; `AddWorkflowActionsFromAssemblyOf<T>(filter)` registers actions as transient (TryAdd).
- Actions are created from `IWorkflowServer.ServiceProvider` (set by `AddStatelessWorkflowEngine`; there is no resolver interface since v6): `Workflow.ExecuteWorkflowAction*` creates a scope per action (sync scopes are disposed via `DisposeAsync`) and uses `ActivatorUtilities.GetServiceOrCreateInstance`, so unregistered actions still get constructor injection. Every action is created by the overridable `CreateWorkflowActionInstance<T>(IServiceProvider scope)` / `CreateWorkflowActionAsyncInstance<T>`, which gets the action scope (null with no provider, when `Activator` is used).
- `WorkflowServer(store, options, loggerFactory)` logs via `[LoggerMessage]` methods in `Logging/WorkflowServerLog` (event ids 1000s) and `Logging/WorkflowNodeMonitorLog` (1100s); the README "Logging" table lists them — keep it in sync.

### Workflow Event Hooks

`OnActionExecuting()`, `OnActionExecuted()`, `OnError()`, `OnSuspend()`, `OnComplete()`. Server events: `WorkflowSuspended`, `WorkflowCompleted`, `WorkflowClaimed`, `WorkflowLockLost`, `NodeEvicted`, `NodeMonitorError`.

### Other Projects

- **`Stateless.WorkflowEngine.WebConsole`** (net10.0) — ASP.NET Core (Kestrel) host and API for the web console: monitoring workflows, managing connections, user auth. See "Web Console" below.
- **`Stateless.WorkflowEngine.WebConsole.Client`** — Vue 3 + Vite + Nuxt UI (JavaScript) SPA, built into the WebConsole's `wwwroot`
- **`Example`** (net10.0) — interactive console app demonstrating all features and stores. Every example builds its services with `Shared/ExampleServices.Create` (DI, console logging, `ValidateOnBuild`); the examples share one assembly, so each scans only its own workflow's action namespace (`InNamespaceOf<T>`)

## Key Patterns

- **Strategy pattern** for store implementations (all implement `IWorkflowStore`)
- **Template method** for workflow configuration (override `Initialise()`)
- **Event-driven**: `WorkflowSuspended`, `WorkflowCompleted`, `WorkflowClaimed`, `WorkflowLockLost`, `NodeEvicted` and `NodeMonitorError` events on `IWorkflowServer`
- **DI support**: `AddStatelessWorkflowEngine()` (see "DI and logging"), setting `IWorkflowServer.ServiceProvider`, or override `CreateWorkflowActionInstance<T>(IServiceProvider)`
- Serialization via Newtonsoft.Json; MongoDB workflows should use `[BsonIgnoreExtraElements]` for forward compatibility
- MongoDB Guids: driver 3 needs an explicit Guid representation. Workflow/definition ids use `MongoDbWorkflowStore.IdGuidRepresentation` (CSharpLegacy, binary subtype 3 — the format existing 2.x-era data uses) via class maps in the store's static constructor, `[BsonGuidRepresentation]` on `MongoWorkflow.Id`/`UIWorkflow.Id`, and `ToBsonId()` for raw `BsonDocument` queries. Never register a global Guid serializer from library code (the driver forbids changing it once used) — apps with Guid properties register it themselves (README "Guids"). `Fixtures/WorkflowSavedByDriver2.json` in the MongoDb tests is a real document saved by driver 2.30

## Target Framework

Every project is SDK-style and targets **net10.0** (from v6; .NET Framework consumers stay on 5.x). Packages use `PackageReference` — there is no `packages.config`, `.nuget` folder or `SharedAssemblyInfo.cs`; versions live in each csproj and are set by `deployment/build.ps1`.

## Web Console

- **Backend** (`source/Stateless.WorkflowEngine.WebConsole`): controllers under `Features/<Area>/` (route prefix `/api`), all inheriting `AuthenticatedBaseController`. Validators are plain classes with `virtual` methods so they can be substituted in tests. Services are registered in `WebConsoleServiceCollectionExtensions.AddWebConsole()`.
- **Auth**: cookie authentication; each claim in `Security/Claims.cs` is also an authorization policy of the same name. Admins get all claims. The principal is rebuilt from the user store on every request.
- **Storage**: users, dashboards and connections live in `{DataPath}/users.json` (format shared with the old Nancy console). Each connection belongs to one dashboard (`ConnectionModel.DashboardId`); there is always exactly one default dashboard (renameable, not deletable) and connections without a valid dashboard are moved onto it on load. Connection passwords use ASP.NET Data Protection (keys in `{DataPath}/keys`); legacy AES-GCM passwords are migrated on load.
- **Stores**: MongoDb and RavenDb (`WorkflowStoreType`). `WorkflowStoreFactory` caches one `MongoClient`/`DocumentStore` per connection (evicted on edit/delete). The console never loads workflow types, so it only uses store methods that work on raw documents (`Get*AsJson`, counts, suspend/unsuspend/delete, definitions); `WorkflowInfoService` parses Mongo JSON with the BSON serializer and RavenDb JSON with Newtonsoft (which ignores the `$type` of the workflow). RavenDb connections have no collections; a secured server uses https with a client certificate (`CertificatePath`, password in `Password`).
- **Hosting**: Windows service (`Stateless.WorkflowEngine.Console`, default `http://localhost:8811`) or Docker (`Dockerfile` in the project, context = `source`). `Update.ps1` is run manually by admins to upgrade; the UI only shows an update-available banner.
- **Frontend** (`source/Stateless.WorkflowEngine.WebConsole.Client`): `composables/use*.js` per area with module-level shared state, `api()` fetch wrapper in `useApi.js`, views under `features/<area>/`.

```bash
# dev: run the API (port 8811) and the Vite dev server (port 5173, proxies /api)
dotnet run --project source/Stateless.WorkflowEngine.WebConsole
cd source/Stateless.WorkflowEngine.WebConsole.Client && npm install && npm run dev

# publish (runs npm ci/build into wwwroot; pass -p:SkipClientBuild=true to skip)
dotnet publish source/Stateless.WorkflowEngine.WebConsole -c Release -r win-x64 --self-contained false
```
