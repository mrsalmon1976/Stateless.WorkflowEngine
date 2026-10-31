# Project Conventions and Operational Gotchas

This quick guide summarizes crucial architectural decision points and command requirements for working on the Workflow Engine.

## Build & Testing
*   **Test Framework:** Uses NUnit 4.1.0 with NSubstitute 5.1.0.
*   **Test Projects Target:** Be aware that the MongoDb, RavenDb and core test projects target **.NET Framework 4.8** and rely on `packages.config` for dependencies, not modern SDK-style approaches. The WebConsole tests are SDK-style **net10.0**.
*   **Running All Tests:** Use `dotnet test source/Stateless.WorkflowEngine.sln` from the solution root.

## Architecture
*   **Mixed Targets:** The codebase uses multiple target frameworks:
    *   Core library: `netstandard2.0`
    *   WebConsole (ASP.NET Core host + Vue/Nuxt UI SPA in `Stateless.WorkflowEngine.WebConsole.Client`): `net10.0`
    *   RavenDb/Tests: `.NET Framework 4.8`
    *   Example demo app: `net8.0`
*   **MongoDb Serialization:** When dealing with MongoDB workflows, always ensure the use of `[BsonIgnoreExtraElements]` for defining classes to maintain forward compatibility.

## Build Commands
*   **Build Solution:** `msbuild source/Stateless.WorkflowEngine.sln` (Standard build).
*   **Publish Releases:** Use the deployment script: `powershell -File deployment/build.ps1`

## Key Patterns
*   The core workflow logic uses the **Strategy pattern** for store implementations (`IWorkflowStore`).
*   Workflow configuration relies on the **Template method** pattern (`Initialise()` override).