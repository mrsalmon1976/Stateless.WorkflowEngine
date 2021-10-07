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

        #region DeleteInstallationTempFolders Tests

        [Test]
        public void DeleteInstallationTempFolders_OnExecute_DeletesShaowCopyFolder()
        {
            _versionUpdateService.ApplicationRootDirectory = AppContext.BaseDirectory;
            string expectedPath = Path.Combine(_versionUpdateService.ApplicationRootDirectory, UpdateConstants.AutoUpdaterShadowCopyFolderName);

            _versionUpdateService.DeleteInstallationTempFolders();

            _fileUtility.Received(1).DeleteDirectoryRecursive(expectedPath);
        }

        [Test]
        public void DeleteInstallationTempFolders_ExceptionThrownWhenDeleting_ContinuesSilently()
        {
            _versionUpdateService.ApplicationRootDirectory = AppContext.BaseDirectory;
            string expectedPath = Path.Combine(_versionUpdateService.ApplicationRootDirectory, UpdateConstants.AutoUpdaterShadowCopyFolderName);
            _fileUtility.When(x => x.DeleteDirectoryRecursive(Arg.Any<string>())).Throw(new Exception());

            _versionUpdateService.DeleteInstallationTempFolders();

            _fileUtility.Received(1).DeleteDirectoryRecursive(expectedPath);
        }

        #endregion

        #region InstallUpdate Tests

        [Test]
        public void InstallUpdate_OnExecute_CopiesToShadowAndRunsProcess()
        {
            // setup
            string applicationRootFolder = AppContext.BaseDirectory;
            string autoUpdaterFolder = Path.Combine(applicationRootFolder, UpdateConstants.AutoUpdaterFolderName);
            string autoUpdaterShadowCopyFolder = Path.Combine(applicationRootFolder, UpdateConstants.AutoUpdaterShadowCopyFolderName);

            IProcessWrapper processWrapper = Substitute.For<IProcessWrapper>();
            _processWrapperFactory.GetProcess().Returns(processWrapper);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            processWrapper.StartInfo.Returns(startInfo);

            // execute
            _versionUpdateService.ApplicationRootDirectory = applicationRootFolder;
            _versionUpdateService.InstallUpdate();

            // assert
            _fileUtility.Received(1).CopyRecursive(autoUpdaterFolder, autoUpdaterShadowCopyFolder);

            Assert.AreEqual(autoUpdaterShadowCopyFolder, processWrapper.StartInfo.WorkingDirectory);
            Assert.AreEqual(UpdateConstants.AutoUpdaterExeFileName, processWrapper.StartInfo.FileName);
            Assert.AreEqual(UpdateConstants.StartInfoVerb, processWrapper.StartInfo.Verb);
            processWrapper.Received(1).Start();
        }


        #endregion
    }
}
