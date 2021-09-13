using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
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
            string webConsoleExeFileName = UpdateConstants.WebConsoleExeFileName;

            // setup - get the location of the webconsole debug folder
            string webConsoleDebugFolder = Path.Combine(
                Directory.GetParent(Assembly.GetExecutingAssembly().Location).Parent.Parent.Parent.FullName,
                "Stateless.WorkflowEngine.WebConsole\\bin\\Debug"
                );

            IUpdateLocationService updateLocationService = Substitute.For<IUpdateLocationService>();
            updateLocationService.ApplicationFolder.Returns(webConsoleDebugFolder);


            // execute
            IWebConsoleVersionService assemblyVersionService = new AssemblyVersionService(webConsoleExeFileName, updateLocationService);
            string webConsoleVersion = assemblyVersionService.GetWebConsoleVersion();

            // work out the version manually
            string pathToExe = Path.Combine(webConsoleDebugFolder, webConsoleExeFileName);
            var versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
            string currentVersion = System.Version.Parse(versionInfo.FileVersion).ToString(3);

            // assert
            Assert.AreEqual(currentVersion, webConsoleVersion);

        }
    }
}
