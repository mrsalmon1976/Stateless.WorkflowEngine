using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version;
using System;
using System.Collections.Generic;
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
            // setup - get the version of the current assembly
            var executingAssembly = Assembly.GetExecutingAssembly();
            string currentVersion = executingAssembly.GetName().Version.ToString(3);

            string assemblyFileName = Path.GetFileName(executingAssembly.Location);

            // execute
            IAssemblyVersionChecker versionChecker = new AssemblyVersionChecker();
            versionChecker.WebConsoleExeFileName = assemblyFileName;
            string webConsoleVersion = versionChecker.GetWebConsoleVersion();

            // assert
            Assert.AreEqual(currentVersion, webConsoleVersion);

        }
    }
}
