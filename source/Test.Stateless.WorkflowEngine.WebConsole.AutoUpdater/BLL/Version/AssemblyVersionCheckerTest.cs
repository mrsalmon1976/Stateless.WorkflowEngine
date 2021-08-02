using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Update;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version
{
    [TestFixture]
    public class AssemblyVersionCheckerTest
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
            updateLocationService.BaseFolder.Returns(webConsoleDebugFolder);


            // execute
            IAssemblyVersionChecker versionChecker = new AssemblyVersionChecker(updateLocationService);
            string webConsoleVersion = versionChecker.GetWebConsoleVersion();

            // work out the version manually
            string pathToExe = Path.Combine(webConsoleDebugFolder, versionChecker.WebConsoleExeFileName);
            var versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
            string currentVersion = System.Version.Parse(versionInfo.FileVersion).ToString(3);

            // assert
            Assert.AreEqual(currentVersion, webConsoleVersion);

        }
    }
}
