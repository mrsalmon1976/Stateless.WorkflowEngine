1. Run "build.ps1", which will
	a. Prompt for the new version number (enter in format 1.2.3)
	b. Automatically update the relevant source files
	c. Build a release version of the solution
	d. Create the zip files (WebConsole, RavenDb, MongoDb) - the WebConsole build requires the .NET 10 SDK and Node.js/npm
	e. Optionally build the WebConsole Docker image
2. Push all changes to GitHub
3. Run publish.ps1 to push the Nuget packages
	* Make sure you have updated all .nuspec files for dependencies - this is a manual process
	* make sure you enter the correct version number when prompted
4. Go to GitHub and add these as new release files
	* The tag name should be the version number, e.g. "2.3.0" (a "v" prefix is tolerated) - this is used by the
	  console's update check and by Update.ps1

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
* In an elevated PowerShell prompt in the install folder, run .\Update.ps1
* Upgrading from the .NET Framework (Nancy/Topshelf) console, version 5.x or earlier:
	* Install the ASP.NET Core 10 runtime first
	* Copy Update.ps1 from the new release zip over the old one before running it (the old script can't install
	  the new service), then run it - Data\users.json is kept, the Topshelf service is re-registered, and saved
	  connection passwords are re-encrypted on first start
	* Back up Data\users.json beforehand - once migrated, connection passwords can't be read by the old console

Web console in Docker
---------------------
* docker build -f source/Stateless.WorkflowEngine.WebConsole/Dockerfile -t stateless-workflowengine-webconsole source
* docker run -d -p 8811:8080 -v webconsole_data:/app/Data stateless-workflowengine-webconsole
* Or use the "webconsole" service in docker-compose.yml.  When connecting to the compose MongoDB service from the
  console, use host "mongodb" (not localhost)
* The /app/Data volume holds users, connections and the data protection keys used to encrypt connection
  passwords - losing the keys means connection passwords must be re-entered
