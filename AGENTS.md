# Project Conventions and Operational Gotchas

This quick guide summarizes crucial architectural decision points and command requirements for working on the Workflow Engine.

## Build & Testing
*   **Test Framework:** Uses NUnit 4.3.2 with NSubstitute 5.3.0.
*   **Running All Tests:** Use `dotnet test source/Stateless.WorkflowEngine.sln` from the solution root. Tests in the `RequiresMongoDb`/`RequiresRavenDb` categories need the containers started by `start.ps1`.

## Architecture
*   **Target Framework:** Every project is SDK-style and targets **net10.0** (libraries, tests, WebConsole and Example). Packages are managed with `PackageReference` - there is no `packages.config` or `.nuget` folder.
*   **WebConsole:** ASP.NET Core host + Vue/Nuxt UI SPA in `Stateless.WorkflowEngine.WebConsole.Client`.
*   **MongoDb Serialization:** When dealing with MongoDB workflows, always ensure the use of `[BsonIgnoreExtraElements]` for defining classes to maintain forward compatibility.
*   **MongoDb Guids:** The store uses MongoDB.Driver 3.x (server 4.4+), which needs an explicit Guid representation. Ids are stored as `CSharpLegacy` (binary subtype 3) to match data saved by earlier versions - see `MongoDbWorkflowStore.IdGuidRepresentation`. Don't register a global Guid serializer from library code.

## Build Commands
*   **Build Solution:** `dotnet build source/Stateless.WorkflowEngine.sln` (Standard build).
*   **Publish Releases:** Use the deployment script: `powershell -File deployment/build.ps1`

## Key Patterns
*   The core workflow logic uses the **Strategy pattern** for store implementations (`IWorkflowStore`).
*   Workflow configuration relies on the **Template method** pattern (`Initialise()` override).