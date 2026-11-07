0. Run the tests (with the start.ps1 containers running for the MongoDb/RavenDb tests) - build.ps1 doesn't:
	dotnet test source\Stateless.WorkflowEngine.sln
1. Run "build.ps1", which will
	a. Prompt for the new version number (format 1.2.3 - it asks again until it is) and an optional pre-release
	   suffix (e.g. alpha1)
	b. Automatically update the version in the project files (only the version lines change)
	c. Build a release version of the solution
	d. Create the zip files (WebConsole, RavenDb, MongoDb) - the WebConsole build requires the .NET 10 SDK and Node.js/npm,
	   and reinstalls the client's node_modules (npm ci), so stop any running Vite dev server first
	e. Optionally build the WebConsole Docker image
	It stops if any publish or zip step fails
2. Commit and push the version changes to GitHub
3. Run publish.ps1 to push the Nuget packages (Stateless.WorkflowEngine, Stateless.WorkflowEngine.MongoDb and
   Stateless.WorkflowEngine.RavenDb)
	* The version is read from the project files set by build.ps1, and confirmed when prompted
	* Package metadata and dependencies come from the .csproj files - there are no .nuspec files to maintain
	* The packages are rebuilt from scratch (deterministic, for source link) before packing
	* A non-production push creates a local feed in NugetTest\NugetPackageTest, which the NugetTestApp restores
	  from - it always uses the newest version in that feed, and re-pushing a version replaces it
4. Go to GitHub and add these as new release files
	* The tag name should be the version number, e.g. "2.3.0" (a "v" prefix is tolerated) - this is used by the
	  console's update check and by Update.ps1
	* Keep the zip names build.ps1 gives them - Update.ps1 downloads the zip with "webconsole" in its name
	* Tick "Set as a pre-release" for pre-release builds - the update check and Update.ps1 use GitHub's latest
	  release, which excludes pre-releases, so installed consoles won't be offered them
	* Publish the release (not a draft) - GitHub records each file's SHA-256 hash, which Update.ps1 verifies

Web console installation (Windows service)
------------------------------------------
* Install the ASP.NET Core 10 runtime (Hosting Bundle) on the server
* Unzip Stateless.WorkflowEngine.WebConsole_v{version}.zip into the install folder
* Optionally create appsettings.Production.json to override settings (e.g. "Urls": "http://*:8811") - this file is
  preserved by Update.ps1
* In an elevated PowerShell prompt, run .\install-service.ps1 (use -Uninstall to remove the service)
* Browse to http://localhost:8811 and sign in with admin/admin - change this password immediately

Web console upgrades
--------------------
* The console shows a banner when a new version is available - upgrades are not triggered from the UI
* In an elevated PowerShell prompt in the install folder, run .\Update.ps1, which
	* Checks the runtime the new release needs is installed before changing anything
	* Backs up the install to Backup\{version} and replaces the application files, keeping Data, logs and
	  appsettings.Production.json - changes made to appsettings.json are overwritten
	* Restores the backup and restarts the service if the update fails, including the new version not starting
	* Replaces itself with the release's copy, so the next update runs the latest script
	* Logs to updates.log in the install folder
* Upgrading from the .NET Framework (Nancy/Topshelf) console, version 5.x or earlier:
	* Install the ASP.NET Core 10 runtime first
	* Copy Update.ps1 from the new release zip over the old one before running it (the old script can't install
	  the new service), then run it - Data\users.json is kept, the Topshelf service is changed to run the new
	  console (keeping its account and startup settings), and saved connection passwords are re-encrypted on
	  first start
	* The old console's port (Port in the .exe.config) isn't carried over - if it wasn't 8811, set "Urls" in
	  appsettings.Production.json
	* Back up Data\users.json beforehand - once migrated, connection passwords can't be read by the old console

Web console in Docker
---------------------
* docker build -f source/Stateless.WorkflowEngine.WebConsole/Dockerfile -t stateless-workflowengine-webconsole source
* docker run -d -p 8811:8080 -v webconsole_data:/app/Data stateless-workflowengine-webconsole
* Or use the "webconsole" service in docker-compose.yml.  When connecting to the compose MongoDB service from the
  console, use host "mongodb" (not localhost)
* The /app/Data volume holds users, connections and the data protection keys used to encrypt connection
  passwords - losing the keys means connection passwords must be re-entered
