using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Diagnostics;
using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class VersionUpdateServiceTest
    {
        private IProcessWrapperFactory _processWrapperFactory;
        private IFileUtility _fileUtility;

        private IVersionUpdateService _versionUpdateService;

        [SetUp]
        public void WorkflowStoreInfoServiceTest_SetUp()
        {
            _processWrapperFactory = Substitute.For<IProcessWrapperFactory>();
            _fileUtility = Substitute.For<IFileUtility>();

            _versionUpdateService = new VersionUpdateService(_processWrapperFactory, _fileUtility);
        }

        #region InstallUpdate Tests

        [Test]
        public void InstallUpdate_OnExecute_CopiesToShadowAndRunsProcess()
        {
            // setup
            string applicationRootFolder = AppContext.BaseDirectory;
            string autoUpdaterFolder = Path.Combine(applicationRootFolder, UpdateConstants.AutoUpdaterFolderName);

            IProcessWrapper processWrapper = Substitute.For<IProcessWrapper>();
            _processWrapperFactory.GetProcess().Returns(processWrapper);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            processWrapper.StartInfo.Returns(startInfo);

            // execute
            _versionUpdateService.ApplicationRootDirectory = applicationRootFolder;
            _versionUpdateService.InstallUpdate();

            // assert
            Assert.That(processWrapper.StartInfo.WorkingDirectory, Is.EqualTo(autoUpdaterFolder));
            Assert.That(processWrapper.StartInfo.FileName, Is.EqualTo(UpdateConstants.AutoUpdaterExeFileName));
            Assert.That(processWrapper.StartInfo.Verb, Is.EqualTo(UpdateConstants.StartInfoVerb));
            processWrapper.Received(1).Start();
        }


        #endregion
    }
}
