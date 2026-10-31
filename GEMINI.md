# Stateless.WorkflowEngine

Stateless.WorkflowEngine is a .NET workflow engine built on top of the [stateless](https://github.com/nblumhardt/stateless) State Machine. It provides a wrapper for managing state transitions with additional workflow features like retry logic, exception handling, and persistence.

## Project Overview

- **Core Engine:** Located in `source/Stateless.WorkflowEngine`.
- **Persistence:** Supports multiple stores:
    - **MemoryStore:** In-memory persistence (good for testing).
    - **MongoDbStore:** Persistence using MongoDB (implemented in `source/Stateless.WorkflowEngine.MongoDb`).
    - **RavenDbStore:** Persistence using RavenDB (implemented in `source/Stateless.WorkflowEngine.RavenDb`).
- **Web Console:** A monitoring interface - an ASP.NET Core (.NET 10) host in `source/Stateless.WorkflowEngine.WebConsole` serving a Vue 3 + Nuxt UI SPA from `source/Stateless.WorkflowEngine.WebConsole.Client`.
- **Example App:** A sample application demonstrating usage is in `source/Example`.

## Architecture & Key Components

- **Workflow:** The base class for defining workflows. Workflows are configured in the `Initialise` override.
- **WorkflowServer:** Processes workflows. Typically runs as a singleton in a background service (e.g., using Topshelf).
- **WorkflowClient:** Used to register workflows for processing by the `WorkflowServer`.
- **WorkflowStore:** Interface for persistence implementations (`IWorkflowStore`).

## Building and Running

### Prerequisites
- .NET 10 SDK (every project targets `net10.0`)
- Node.js/npm (only for publishing the web console client)
- PowerShell (for deployment scripts)

### Build Commands
You can build the solution from the `source` directory:
```powershell
dotnet build Stateless.WorkflowEngine.sln
```

Alternatively, use the provided build script in the `deployment` folder:
```powershell
.\deployment\build.ps1
```

### Running the Web Console
The Web Console runs as a Windows service (or Docker container) in production. For development, run the API and the Vite dev server (which proxies `/api` to the API):
```powershell
dotnet run --project source\Stateless.WorkflowEngine.WebConsole      # http://localhost:8811
cd source\Stateless.WorkflowEngine.WebConsole.Client; npm install; npm run dev   # http://localhost:5173
```

### Testing
Tests are written using NUnit and can be found in the `source/Test.*` projects. Run them with `dotnet test source/Stateless.WorkflowEngine.sln` or through Visual Studio. The `RequiresMongoDb`/`RequiresRavenDb` tests need the containers started by `start.ps1`.

## Development Conventions

- **Dependency Injection:** Most classes implement interfaces (`IWorkflowServer`, `IWorkflowClient`, etc.). A custom `IWorkflowEngineDependencyResolver` can be used to integrate with your preferred DI container.
- **Workflow Configuration:** All state machine configuration should be done in the `Initialise` method of your custom `Workflow` class.
- **Persistence Models:** Active workflows are stored in a `Workflows` collection, and completed ones are moved to `CompletedWorkflows`.
- **Events:** The `WorkflowServer` and `Workflow` classes expose several events/overrides for lifecycle hooks (e.g., `WorkflowSuspended`, `OnActionExecuting`, `OnError`).

## Directory Structure

- `source/`: Contains the main solution and project files.
- `deployment/`: Contains build and packaging scripts.
- `docs/`: Documentation and example files.
