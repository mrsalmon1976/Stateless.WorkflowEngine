1. Update the version number in SharedAssemblyInfo.cs
2. Run "build.ps1" - this will create the zip files - make sure you enter the version number correctly e.g. "2.3.0"
3. Run publish.ps1 to push the Nuget packages - make sure you enter the correct version number when prompted
4. Go to GitHub and add these as new release files
	The tag name MUST be the same as the version number, e.g. "2.3.0" - this is used by the AutoUpdater
