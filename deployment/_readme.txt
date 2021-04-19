1. Set the version number in source/SharedAssemblyVersion.cs to 2.x.x
2. Set the version number in the Stateless.WorkflowEngine and Stateless.WorkflowEngine.MongoDb projects
3. Build the project in Debug and Release
4. Run "build.ps1" - this will create the zip files
5. Go to GitHub and add these as new release files
6. Run publish.ps1 - make sure you enter the correct version number when prompted