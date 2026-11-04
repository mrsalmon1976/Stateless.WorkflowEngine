# Development

How to set up a machine to work on Stateless.WorkflowEngine, run it, and debug it.  For using the library, see the [README](README.md).

## Prerequisites

| Tool | Used for |
|------|----------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Building everything |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | MongoDB, RavenDB, the web console container and the Aspire dashboard |
| [Node.js](https://nodejs.org/) 20.19+ or 22.12+ (includes npm) | The web console client (Vue 3 + Vite) |
| Visual Studio 2022 or later | Running and debugging the .NET projects |
| VS Code (optional) | Running and debugging the web console client |

All scripts are PowerShell and are run from the repository root.

`start.ps1` asks what to start:

```
  1. Everything in Docker, rebuilding the console image if the source has changed
  2. Everything in Docker, without rebuilding the console image
  3. Console from source with live reload (API and client windows)
  4. Client only with live reload - run the API yourself, e.g. to debug it in Visual Studio
  5. Stop the containers
```

Each option also has a switch that skips the menu - `-Full`, `-NoBuild`, `-Dev`, `-ClientOnly` and `-Stop`.

## Repository layout

| Folder | Contents |
|--------|----------|
| `source/Stateless.WorkflowEngine.sln` | The engine, the stores, the Example app, the web console API and the tests |
| `source/Stateless.WorkflowEngine.WebConsole` | The web console's ASP.NET Core host and API |
| `source/Stateless.WorkflowEngine.WebConsole.Client` | The web console's frontend source (Vue 3 + Vite).  It isn't part of the solution - it is built into the WebConsole project's `wwwroot`, which is build output and should not be edited |
| `source/Example` | Interactive console app demonstrating the engine against MongoDB or RavenDB |
| `start.ps1`, `docker-compose.yml` | The local environment |

## Running the project normally

This runs everything in Docker, with no debugging - useful for trying the console or running the examples and tests.

1. Run `.\start.ps1` and choose **1** (or **2** to skip rebuilding the console image).  This starts Docker Desktop if needed, then starts these containers:

   | Service | Address |
   |---------|---------|
   | Web console | http://localhost:8811 - sign in with **admin / admin** on first run |
   | MongoDB | `mongodb://localhost:27017` |
   | RavenDB studio | http://localhost:8080 |
   | Aspire dashboard (metrics, traces and logs) | http://localhost:18888 |

   On first run, connections to the example MongoDB and RavenDB databases are added to the console.

2. Create some workflows with the Example app:

   ```powershell
   dotnet run --project source\Example
   ```

   Choose a store and an example from the menu.  The workflows show in the web console, and the engine's metrics, traces and logs in the Aspire dashboard.

3. Run the tests:

   ```powershell
   dotnet test source\Stateless.WorkflowEngine.sln
   dotnet test source\Stateless.WorkflowEngine.sln --filter "TestCategory!=RequiresMongoDb&TestCategory!=RequiresRavenDb"   # no databases needed
   ```

   The MongoDB and RavenDB store tests use the containers.  To use other servers, set `STATELESS_MONGODB_CONNECTIONSTRING` / `STATELESS_RAVENDB_URL` (these also apply to the Example app).

4. When you're done, run `.\start.ps1` and choose **5** to stop the containers (data is kept in Docker volumes).

The console container is a baked image, so it only shows source changes after option 1 rebuilds it.  To work on the console, use one of the modes below.

## Working on the web console with live reload

Run `.\start.ps1` and choose **3**.  The console container is stopped and the console runs from source in two new windows:

* **API** - `dotnet watch` on http://localhost:8811, which restarts or hot reloads on C# changes
* **Client** - the Vite dev server on http://localhost:5173, which reloads the browser on frontend changes and passes `/api` calls to the API

Browse to **http://localhost:5173**.  http://localhost:8811 still works, but serves the last built copy of the client from `wwwroot`, not the live source.

The console run from source keeps its users and connections in `source/Stateless.WorkflowEngine.WebConsole/Data`, separate from the container's.  Close the two windows to stop it.

## Debugging

### The web console (API in Visual Studio, client in VS Code)

1. Run `.\start.ps1` and choose **4** - this starts the databases, stops the console container so port 8811 is free, and opens a window running `npm run dev` in the Client folder.

2. **API** - in Visual Studio, open `source/Stateless.WorkflowEngine.sln`, set `Stateless.WorkflowEngine.WebConsole` as the startup project and press F5 (the `WebConsole` profile, http://localhost:8811).  Breakpoints in controllers and services are hit as normal.

3. **Client** - in VS Code, open the `source` folder, go to *Run and Debug* and start **Web console client (Vite already running)**.  Edge opens on http://localhost:5173 with the debugger attached, and breakpoints in the `.vue` and `.js` files under `Stateless.WorkflowEngine.WebConsole.Client/src` are hit.

   The other configuration, **Web console client (start Vite)**, starts Vite itself - use it if you didn't start the client with `start.ps1`.

Because the client calls the API through Vite, you can step from a click in the browser through to the controller handling the request.

If option 4 ran before the API was up, it skips adding the example database connections - run it again once the API is running, or add them in the console.

### The engine and the Example app

The Example app uses the containers, so start them first (`.\start.ps1` option 1, or option 4 if you're also debugging the console).  Then in Visual Studio, set `Example` as the startup project and press F5.  The engine is referenced as a project, so breakpoints in `Stateless.WorkflowEngine` and the store projects are hit too.

### Tests

Debug tests from Visual Studio's Test Explorer.  The `RequiresMongoDb` / `RequiresRavenDb` tests need the containers running.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| The API in Visual Studio fails to start because port 8811 is in use | The console container (or an option 3 window) is running.  Run `.\start.ps1` and choose option 4 (or run `docker compose stop webconsole`), and close any *Web console API (dotnet watch)* window |
| The Example app's build fails because files are locked | Visual Studio is running the Example - stop debugging first |
| Frontend changes don't show | You're on http://localhost:8811 (built files) - use http://localhost:5173 |
| VS Code's *start Vite* configuration waits forever on the pre-launch task | Start Vite with `.\start.ps1` option 4 and use **Web console client (Vite already running)** |
| Nothing appears in the Aspire dashboard | Only the Example app exports telemetry, to `localhost:4317` - check the `stateless_aspire_dashboard` container is running, or set `STATELESS_OTLP_ENDPOINT` |
