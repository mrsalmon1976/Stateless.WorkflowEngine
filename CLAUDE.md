# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Stateless.WorkflowEngine is a .NET workflow engine built as a wrapper around the [Stateless](https://github.com/nblumhardt/stateless) state machine library. It adds persistence, retry mechanisms, exception handling, and workflow suspension on top of Stateless. Current version: 4.1.1.

## Build Commands

```bash
# Build the solution
msbuild source/Stateless.WorkflowEngine.sln

# Build via deployment script (finds MSBuild automatically, packages releases)
powershell -File deployment/build.ps1

# Publish NuGet packages
powershell -File deployment/publish.ps1
```

## Testing

- **Framework**: NUnit 4.1.0 with NSubstitute 5.1.0 for mocking
- **Test projects** target .NET Framework 4.8 and use `packages.config` (not SDK-style)

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
| MemoryWorkflowStore | Core library | netstandard2.0 | — |
| MongoDbWorkflowStore | `Stateless.WorkflowEngine.MongoDb` | netstandard2.0 | MongoDB.Driver 2.26.0 |
| RavenDbWorkflowStore | `Stateless.WorkflowEngine.RavenDb` | net4.8 | RavenDB.Client 5.4.210 |

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

- **`Stateless.WorkflowEngine.WebConsole`** (net4.8) — Nancy-based web console for monitoring workflows, managing connections, user auth
- **`Example`** (net6.0) — interactive console app demonstrating all features and stores

## Key Patterns

- **Strategy pattern** for store implementations (all implement `IWorkflowStore`)
- **Template method** for workflow configuration (override `Initialise()`)
- **Event-driven**: `WorkflowSuspended` and `WorkflowCompleted` events on `IWorkflowServer`
- **DI support**: `IWorkflowEngineDependencyResolver` or override `CreateWorkflowActionInstance<T>()`
- Serialization via Newtonsoft.Json; MongoDB workflows should use `[BsonIgnoreExtraElements]` for forward compatibility

## Mixed Target Frameworks

The core library targets **netstandard2.0**. The test projects, WebConsole, and RavenDb store target **.NET Framework 4.8** and use legacy `packages.config` NuGet management. The Example project targets **net6.0**.
