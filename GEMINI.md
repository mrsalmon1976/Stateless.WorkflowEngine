# Stateless.WorkflowEngine

Stateless.WorkflowEngine is a .NET workflow engine built on top of the [stateless](https://github.com/nblumhardt/stateless) State Machine. It provides a wrapper for managing state transitions with additional workflow features like retry logic, exception handling, and persistence.

## Project Overview

- **Core Engine:** Located in `source/Stateless.WorkflowEngine`.
- **Persistence:** Supports multiple stores:
    - **MemoryStore:** In-memory persistence (good for testing).
    - **MongoDbStore:** Persistence using MongoDB (implemented in `source/Stateless.WorkflowEngine.MongoDb`).
    - **RavenDbStore:** Persistence using RavenDB (implemented in `source/Stateless.WorkflowEngine.RavenDb`).
- **Web Console:** A self-hosted monitoring interface using Nancy, located in `source/Stateless.WorkflowEngine.WebConsole`.
- **Example App:** A sample application demonstrating usage is in `source/Example`.

## Architecture & Key Components

- **Workflow:** The base class for defining workflows. Workflows are configured in the `Initialise` override.
- **WorkflowServer:** Processes workflows. Typically runs as a singleton in a background service (e.g., using Topshelf).
- **WorkflowClient:** Used to register workflows for processing by the `WorkflowServer`.
- **WorkflowStore:** Interface for persistence implementations (`IWorkflowStore`).

## Building and Running

### Prerequisites
- .NET SDK / Visual Studio
- MSBuild (for build scripts)
- PowerShell (for deployment scripts)

### Build Commands
The project uses MSBuild. You can build the solution from the `source` directory:
```powershell
msbuild Stateless.WorkflowEngine.sln
```

Alternatively, use the provided build script in the `deployment` folder:
```powershell
.\deployment\build.ps1
```

### Running the Web Console
The Web Console is a Topshelf-hosted service. You can run it as a console application for development:
```powershell
cd source\Stateless.WorkflowEngine.WebConsole\bin\Debug
.\Stateless.WorkflowEngine.WebConsole.exe
```

### Testing
Tests are written using NUnit and can be found in the `source/Test.*` projects. You can run them using the NUnit test runner or through Visual Studio.

## Development Conventions

- **Dependency Injection:** Most classes implement interfaces (`IWorkflowServer`, `IWorkflowClient`, etc.). A custom `IWorkflowEngineDependencyResolver` can be used to integrate with your preferred DI container.
- **Workflow Configuration:** All state machine configuration should be done in the `Initialise` method of your custom `Workflow` class.
- **Persistence Models:** Active workflows are stored in a `Workflows` collection, and completed ones are moved to `CompletedWorkflows`.
- **Events:** The `WorkflowServer` and `Workflow` classes expose several events/overrides for lifecycle hooks (e.g., `WorkflowSuspended`, `OnActionExecuting`, `OnError`).

## Directory Structure

- `source/`: Contains the main solution and project files.
- `deployment/`: Contains build and packaging scripts.
- `docs/`: Documentation and example files.
- `packages/`: NuGet packages.
