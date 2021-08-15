using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    [TestFixture]
    public class AssemblyVersionServiceTest
    {
        [Test]
        public void GetWebConsoleVersion_GivenExe_ReturnsAssemblyVersion()
        {
            // setup - get the location of the webconsole debug folder
            string webConsoleDebugFolder = Path.Combine(
                Directory.GetParent(Assembly.GetExecutingAssembly().Location).Parent.Parent.Parent.FullName,
                "Stateless.WorkflowEngine.WebConsole\\bin\\Debug"
                );

            IUpdateLocationService updateLocationService = Substitute.For<IUpdateLocationService>();
            updateLocationService.ApplicationFolder.Returns(webConsoleDebugFolder);


            // execute
            IAssemblyVersionService assemblyVersionService = new AssemblyVersionService(updateLocationService);
            string webConsoleVersion = assemblyVersionService.GetWebConsoleVersion();

            // work out the version manually
            string pathToExe = Path.Combine(webConsoleDebugFolder, assemblyVersionService.WebConsoleExeFileName);
            var versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
            string currentVersion = System.Version.Parse(versionInfo.FileVersion).ToString(3);

            // assert
            Assert.AreEqual(currentVersion, webConsoleVersion);

        }
    }
}
