using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    [TestFixture]
    public class UpdateFileServiceTest
    {

        private IUpdateFileService _updateFileService;

        private IUpdateLocationService _updateLocationService;
        private IFileUtility _fileUtility;

        [SetUp]
        public void SetUp_UpdateFileServiceTest()
        {
            _updateLocationService = Substitute.For<IUpdateLocationService>();
            _fileUtility = Substitute.For<IFileUtility>();

            _updateFileService = new UpdateFileService(_updateLocationService, _fileUtility);
        }

        [Test]
        public void Backup_OnExecute_DeletesAndCopiesRecursively()
        {
            // setup
            string backupFolder = GetFakePath("Backup");
            string applicationFolder = GetFakePath("AppFolder");
            _updateLocationService.BackupFolder.Returns(backupFolder);
            _updateLocationService.ApplicationFolder.Returns(applicationFolder);

            // execute
            _updateFileService.Backup().Wait();

            // assert
            _fileUtility.Received(1).DeleteDirectoryRecursive(backupFolder);
            _fileUtility.Received(1).CopyRecursive(applicationFolder, backupFolder, Arg.Any<IEnumerable<string>>());
        }

        [Test]
        public void Backup_OnExecute_ExcludesBackupAndTempFolder()
        {
            // setup
            string backupFolder = GetFakePath("Backup");
            string applicationFolder = GetFakePath("AppFolder");
            string updateTempFolder = GetFakePath("UpdateTemp");
            _updateLocationService.BackupFolder.Returns(backupFolder);
            _updateLocationService.ApplicationFolder.Returns(applicationFolder);
            _updateLocationService.UpdateTempFolder.Returns(updateTempFolder);


            IEnumerable<string> receivedExclusions = null;
            _fileUtility.CopyRecursive(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<IEnumerable<string>>(x => receivedExclusions = x));

            // execute
            _updateFileService.Backup().Wait();

            // assert
            _fileUtility.Received(1).CopyRecursive(applicationFolder, backupFolder, Arg.Any<IEnumerable<string>>());
            Assert.That(receivedExclusions.Contains(backupFolder), Is.True);
            Assert.That(receivedExclusions.Contains(updateTempFolder), Is.True);

        }

        [Test]
        public void CopyNewVersionFiles_OnExecute_CopiesFilesFromTempFolderToInstallationFolder()
        {
            string newVersionFileName = Path.GetRandomFileName() + ".zip";
            string applicationFolder = GetFakePath("AppFolder");
            string updateTempFolder = GetFakePath("UpdateTemp");
            _updateLocationService.ApplicationFolder.Returns(applicationFolder);
            _updateLocationService.UpdateTempFolder.Returns(updateTempFolder);

            // execute
            _updateFileService.CopyNewVersionFiles(newVersionFileName).Wait();

            // assert
            _fileUtility.Received(1).CopyRecursive(updateTempFolder, applicationFolder, Arg.Any<string[]>());
        }

        [Test]
        public void DeleteCurrentVersionFiles_OnExecute_DeletesContentsOfApplicationFolder()
        {
            // setup
            string applicationFolder = GetFakePath("AppFolder");
            _updateLocationService.ApplicationFolder.Returns(applicationFolder);

            // execute
            _updateFileService.DeleteCurrentVersionFiles().Wait();

            // assert
            _fileUtility.Received(1).DeleteContents(applicationFolder, Arg.Any<IEnumerable<string>>());
        }

        [Test]
        public void DeleteCurrentVersionFiles_OnExecute_ExcludesTemporaryAndDataFolders()
        {
            // setup
            string backupFolder = GetFakePath("Backup");
            string applicationFolder = GetFakePath("AppFolder");
            string updateTempFolder = GetFakePath("UpdateTemp");
            string dataFolder = GetFakePath("DataVault");
            string updateEventLogFilePath = GetFakePath("Update.log");
            string autoUpdaterFolder = GetFakePath("AutoUpdater");
            _updateLocationService.BackupFolder.Returns(backupFolder);
            _updateLocationService.ApplicationFolder.Returns(applicationFolder);
            _updateLocationService.UpdateTempFolder.Returns(updateTempFolder);
            _updateLocationService.DataFolder.Returns(dataFolder);
            _updateLocationService.UpdateEventLogFilePath.Returns(updateEventLogFilePath);
            _updateLocationService.AutoUpdaterFolder.Returns(autoUpdaterFolder);


            IEnumerable<string> receivedExclusions = null;
            _fileUtility.DeleteContents(Arg.Any<string>(), Arg.Do<IEnumerable<string>>(x => receivedExclusions = x));

            // execute
            _updateFileService.DeleteCurrentVersionFiles().Wait();

            // assert
            _fileUtility.Received(1).DeleteContents(applicationFolder, Arg.Any<IEnumerable<string>>());
            Assert.That(receivedExclusions.Contains(backupFolder), Is.True);
            Assert.That(receivedExclusions.Contains(updateTempFolder), Is.True);
            Assert.That(receivedExclusions.Contains(dataFolder), Is.True);
            Assert.That(receivedExclusions.Contains(updateEventLogFilePath), Is.True);
            Assert.That(receivedExclusions.Contains(autoUpdaterFolder), Is.True);
            Assert.That(receivedExclusions.Count(), Is.EqualTo(5));

        }

        [Test]
        public void ExtractReleasePackage_OnExecute_ExtractsZipFile()
        {
            // setup
            string zipFilePath = GetFakePath("Release.zip");
            string extractFolder = GetFakePath("ExtractFolder");

            // execute
            _updateFileService.ExtractReleasePackage(zipFilePath, extractFolder).Wait();

            // assert
            _fileUtility.Received(1).ExtractZipFile(zipFilePath, extractFolder);
        }

        [Test]
        public void ExtractReleasePackage_OnExecute_RenamesAutoUpdaterFilesWithTempExtension()
        {
            // setup
            string zipFilePath = GetFakePath("Release.zip");
            string extractFolder = GetFakePath("ExtractFolder");
            string autoUpdateFolder = Path.Combine(extractFolder, UpdateConstants.AutoUpdaterFolderName);

            string file1 = Path.Combine(autoUpdateFolder, "file1.txt");
            string file2 = Path.Combine(autoUpdateFolder, "file2.txt");
            string[] autoUpdaterFiles = { file1, file2 };
            _fileUtility.DirectoryExists(autoUpdateFolder).Returns(true);
            _fileUtility.GetFiles(autoUpdateFolder, SearchOption.AllDirectories).Returns(autoUpdaterFiles);

            // execute
            _updateFileService.ExtractReleasePackage(zipFilePath, extractFolder).Wait();

            // assert
            _fileUtility.Received(1).DirectoryExists(autoUpdateFolder);
            _fileUtility.Received(1).GetFiles(autoUpdateFolder, SearchOption.AllDirectories);
            _fileUtility.Received(1).MoveFile(file1, $"{file1}{UpdateConstants.AutoUpdaterNewFileExtension}", true);
            _fileUtility.Received(1).MoveFile(file2, $"{file2}{UpdateConstants.AutoUpdaterNewFileExtension}", true);
        }

        [Test]
        public void ExtractReleasePackage_OnExecute_DeletesZipFile()
        {
            // setup
            string zipFilePath = GetFakePath("Release.zip");
            string extractFolder = GetFakePath("ExtractFolder");

            // execute
            _updateFileService.ExtractReleasePackage(zipFilePath, extractFolder).Wait();

            // assert
            _fileUtility.Received(1).DeleteFile(zipFilePath);
        }

        private string GetFakePath(string fileOrFolderName)
        {
            return Path.Combine(Assembly.GetExecutingAssembly().Location, fileOrFolderName);
        }

    }
}
