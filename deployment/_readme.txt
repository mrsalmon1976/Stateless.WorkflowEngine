1. Run "build.ps1", which will
	a. Prompt for the new version number (enter in format 1.2.3) 
	b. Automatically update the relevant source files
	c. Build a release version of the solution
	d. Create the zip files (WebConsole, RavenDb, MongoDb) 
3. Run publish.ps1 to push the Nuget packages
	* Make sure you have updated all .nuspec files for dependencies - this is a manual process
	* make sure you enter the correct version number when prompted
4. Go to GitHub and add these as new release files
	* The tag name MUST be the same as the version number, e.g. "2.3.0" - this is used by the AutoUpdater
