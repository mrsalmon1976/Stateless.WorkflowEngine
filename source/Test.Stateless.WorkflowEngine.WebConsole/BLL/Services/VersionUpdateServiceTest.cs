using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Diagnostics;
using System;
using System.Diagnostics;
using System.IO;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class VersionUpdateServiceTest
    {
        private IProcessWrapperFactory _processWrapperFactory;

        private IVersionUpdateService _versionUpdateService;

        [SetUp]
        public void WorkflowStoreInfoServiceTest_SetUp()
        {
            _processWrapperFactory = Substitute.For<IProcessWrapperFactory>();

            _versionUpdateService = new VersionUpdateService(_processWrapperFactory);
        }

        #region InstallUpdate Tests

        [Test]
        public void InstallUpdate_OnExecute_RunsProcess()
        {
            // setup
            string applicationRootFolder = AppContext.BaseDirectory;
            string scriptPath = Path.Combine(applicationRootFolder, UpdateConstants.UpdaterFileName);

            IProcessWrapper processWrapper = Substitute.For<IProcessWrapper>();
            _processWrapperFactory.GetProcess().Returns(processWrapper);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            processWrapper.StartInfo.Returns(startInfo);

            // execute
            _versionUpdateService.ApplicationRootDirectory = applicationRootFolder;
            _versionUpdateService.InstallUpdate();

            // assert
            Assert.That(processWrapper.StartInfo.FileName, Is.EqualTo("powershell.exe"));
            Assert.That(processWrapper.StartInfo.Arguments, Is.EqualTo($"-ExecutionPolicy Bypass -File \"{scriptPath}\""));
            Assert.That(processWrapper.StartInfo.UseShellExecute, Is.False);
            Assert.That(processWrapper.StartInfo.RedirectStandardOutput, Is.True);
            Assert.That(processWrapper.StartInfo.RedirectStandardError, Is.True);
            Assert.That(processWrapper.StartInfo.CreateNoWindow, Is.True);
            Assert.That(processWrapper.StartInfo.WorkingDirectory, Is.EqualTo(applicationRootFolder));
            Assert.That(processWrapper.StartInfo.Verb, Is.EqualTo(UpdateConstants.StartInfoVerb));
            processWrapper.Received(1).Start();
        }


        #endregion
    }
}
