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

- **Framework**: NUnit 4.3.2 with NSubstitute 5.3.0 for mocking
- **Test projects** are SDK-style net10.0. `Test.Stateless.WorkflowEngine.MongoDb`/`.RavenDb` reference `Test.Stateless.WorkflowEngine` for the shared `WorkflowStoreTestBase`; `Test.Stateless.WorkflowEngine.WebConsole` includes `WebApplicationFactory` integration tests

`start.ps1` (repo root) starts MongoDB (27017), RavenDB (8080) and the web console (8811) in Docker via `docker-compose.yml`. The Example app and the `RequiresMongoDb`/`RequiresRavenDb` tests use these containers by default (override with `STATELESS_MONGODB_CONNECTIONSTRING` / `STATELESS_RAVENDB_URL`).

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
- **`WorkflowServer`** — processes queued workflows. Fetches by Priority DESC, then CreatedOn ASC. Supports concurrent execution via `maxConcurrent` parameter. Should be a singleton.
- **`WorkflowClient`** — registers workflows for processing by the server.
- **`IWorkflowStore`** — persistence abstraction. Two collections: `Workflows` (active) and `CompletedWorkflows` (archived).
- **`IWorkflowAction`** — interface for actions executed at each workflow step.

### Store Implementations

| Store | Project | Target | Key Dependency |
|-------|---------|--------|---------------|
| MemoryWorkflowStore | Core library | net10.0 | — |
| MongoDbWorkflowStore | `Stateless.WorkflowEngine.MongoDb` | net10.0 | MongoDB.Driver 2.30.0 |
| RavenDbWorkflowStore | `Stateless.WorkflowEngine.RavenDb` | net10.0 | RavenDB.Client 7.2.2 |

### Workflow Lifecycle

1. Custom workflow registered via `WorkflowClient.Register()`
2. `WorkflowServer` picks it up and calls `ExecuteWorkflow()`
3. Gets next action, fires Stateless trigger, action executes
4. On completion → archived to `CompletedWorkflows`
5. On error → retry with configurable intervals (default: 5, 10, 15, 30, 60 seconds)
6. After max retries → workflow suspends

### Workflow Event Hooks

`OnActionExecuting()`, `OnActionExecuted()`, `OnError()`, `OnSuspend()`, `OnComplete()`

### Other Projects

- **`Stateless.WorkflowEngine.WebConsole`** (net10.0) — ASP.NET Core (Kestrel) host and API for the web console: monitoring workflows, managing connections, user auth. See "Web Console" below.
- **`Stateless.WorkflowEngine.WebConsole.Client`** — Vue 3 + Vite + Nuxt UI (JavaScript) SPA, built into the WebConsole's `wwwroot`
- **`Example`** (net10.0) — interactive console app demonstrating all features and stores

## Key Patterns

- **Strategy pattern** for store implementations (all implement `IWorkflowStore`)
- **Template method** for workflow configuration (override `Initialise()`)
- **Event-driven**: `WorkflowSuspended` and `WorkflowCompleted` events on `IWorkflowServer`
- **DI support**: `IWorkflowEngineDependencyResolver` or override `CreateWorkflowActionInstance<T>()`
- Serialization via Newtonsoft.Json; MongoDB workflows should use `[BsonIgnoreExtraElements]` for forward compatibility

## Target Framework

Every project is SDK-style and targets **net10.0** (from v6; .NET Framework consumers stay on 5.x). Packages use `PackageReference` — there is no `packages.config`, `.nuget` folder or `SharedAssemblyInfo.cs`; versions live in each csproj and are set by `deployment/build.ps1`.

## Web Console

- **Backend** (`source/Stateless.WorkflowEngine.WebConsole`): controllers under `Features/<Area>/` (route prefix `/api`), all inheriting `AuthenticatedBaseController`. Validators are plain classes with `virtual` methods so they can be substituted in tests. Services are registered in `WebConsoleServiceCollectionExtensions.AddWebConsole()`.
- **Auth**: cookie authentication; each claim in `Security/Claims.cs` is also an authorization policy of the same name. Admins get all claims. The principal is rebuilt from the user store on every request.
- **Storage**: users and connections live in `{DataPath}/users.json` (format shared with the old Nancy console). Connection passwords use ASP.NET Data Protection (keys in `{DataPath}/keys`); legacy AES-GCM passwords are migrated on load.
- **Hosting**: Windows service (`Stateless.WorkflowEngine.Console`, default `http://localhost:8811`) or Docker (`Dockerfile` in the project, context = `source`). `Update.ps1` is run manually by admins to upgrade; the UI only shows an update-available banner.
- **Frontend** (`source/Stateless.WorkflowEngine.WebConsole.Client`): `composables/use*.js` per area with module-level shared state, `api()` fetch wrapper in `useApi.js`, views under `features/<area>/`.

```bash
# dev: run the API (port 8811) and the Vite dev server (port 5173, proxies /api)
dotnet run --project source/Stateless.WorkflowEngine.WebConsole
cd source/Stateless.WorkflowEngine.WebConsole.Client && npm install && npm run dev

# publish (runs npm ci/build into wwwroot; pass -p:SkipClientBuild=true to skip)
dotnet publish source/Stateless.WorkflowEngine.WebConsole -c Release -r win-x64 --self-contained false
```
