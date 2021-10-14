1. Set the version number in source/SharedAssemblyVersion.cs e.g. 2.3.1
2. Set the version number in the Stateless.WorkflowEngine and Stateless.WorkflowEngine.MongoDb projects
	- Package tab under the project properties
3. Build the project in Debug and Release
4. Run "build.ps1" - this will create the zip files
5. Go to GitHub and add these as new release files
	The tag name MUST be the same as the version number, e.g. "2.3.0" - this is used by the AutoUpdater
6. Run publish.ps1 - make sure you enter the correct version number when prompted