using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using Stateless.WorkflowEngine.WebConsole.Jobs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Jobs
{
    [TestFixture]
    public class AutoUpdaterCleanupJobTest
    {
        private AutoUpdaterCleanupJob _autoUpdaterCleanupJob;

        private string _applicationRootPath;
        private IFileUtility _fileUtility;

        [SetUp]
        public void AutoUpdaterCleanupJobTest_SetUp()
        {
            _applicationRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _fileUtility = Substitute.For<IFileUtility>();

            _autoUpdaterCleanupJob = new AutoUpdaterCleanupJob(_applicationRootPath, _fileUtility);
        }

        #region CleanupTempFiles Tests

        [Test]
        public void CleanupTempFiles_Cancelled_Exits()
        {
            // setup 
            DoWorkEventArgs doWorkEventArgs = new DoWorkEventArgs(null);
            doWorkEventArgs.Cancel = true;

            // execute
            _autoUpdaterCleanupJob.CleanUpTempFiles(null, doWorkEventArgs);

            // assert
            _fileUtility.DidNotReceive().DirectoryExists(Arg.Any<string>());
            _fileUtility.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<SearchOption>(), Arg.Any<string>());
            _fileUtility.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public void CleanupTempFiles_DirectoryDoesNotExist_Exits()
        {
            // setup 
            _fileUtility.DirectoryExists(Arg.Any<string>()).Returns(false);

            // execute
            _autoUpdaterCleanupJob.CleanUpTempFiles(null, new DoWorkEventArgs(null));

            // assert
            _fileUtility.Received(1).DirectoryExists(_autoUpdaterCleanupJob.AutoUpdaterPath);
            _fileUtility.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<SearchOption>(), Arg.Any<string>());
            _fileUtility.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public void CleanupTempFiles_NoNewFilesToRename_Exits()
        {
            // setup 
            _fileUtility.DirectoryExists(Arg.Any<string>()).Returns(true);
            _fileUtility.GetFiles(_autoUpdaterCleanupJob.AutoUpdaterPath, SearchOption.AllDirectories, "*" + UpdateConstants.AutoUpdaterNewFileExtension).Returns(new string[] { });

            // execute
            _autoUpdaterCleanupJob.CleanUpTempFiles(null, new DoWorkEventArgs(null));

            // assert
            _fileUtility.Received(1).DirectoryExists(_autoUpdaterCleanupJob.AutoUpdaterPath);
            _fileUtility.Received().GetFiles(_autoUpdaterCleanupJob.AutoUpdaterPath, SearchOption.AllDirectories, "*" + UpdateConstants.AutoUpdaterNewFileExtension);
            _fileUtility.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public void CleanupTempFiles_NewFilesExist_FilesAreRenamedToExcludeNewExtension()
        {
            // setup 
            _fileUtility.DirectoryExists(Arg.Any<string>()).Returns(true);

            string file1 = Path.Combine(_autoUpdaterCleanupJob.AutoUpdaterPath, "file1.txt.new");
            string file2 = Path.Combine(_autoUpdaterCleanupJob.AutoUpdaterPath, "file2.txt.new");
            string file3 = Path.Combine(_autoUpdaterCleanupJob.AutoUpdaterPath, "file3.txt.new");

            // set it up so the first time it is called, files are found, the second time none are found
            int i = 0;
            string[] fileArray = { file1, file2, file3 };
            string[] noFileArray = { };
            _fileUtility
                .GetFiles(Arg.Any<string>(), Arg.Any<SearchOption>(), Arg.Any<string>())
                .Returns(x => { return (i == 1 ? fileArray : noFileArray ); })
                .AndDoes((ci) => { i++; });

            // execute
            _autoUpdaterCleanupJob.CleanUpTempFiles(null, new DoWorkEventArgs(null));

            // assert
            _fileUtility.Received(2).GetFiles(Arg.Any<string>(), SearchOption.AllDirectories, "*.new");
            _fileUtility.Received(3).MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
            _fileUtility.Received(1).MoveFile(file1, file1.Replace(".new", ""), true);
            _fileUtility.Received(1).MoveFile(file2, file2.Replace(".new", ""), true);
            _fileUtility.Received(1).MoveFile(file3, file3.Replace(".new", ""), true);
        }

        [Test]
        public void CleanupTempFiles_WhenRenameFails_ThrowsExceptionAndTriesAgain()
        {
            // setup 
            _fileUtility.DirectoryExists(Arg.Any<string>()).Returns(true);

            string file1 = Path.Combine(_autoUpdaterCleanupJob.AutoUpdaterPath, "file1.txt.new");

            // set it up so the first time it is called, files are found, the second time none are found
            int i = 0;
            string[] fileArray = { file1 };
            string[] noFileArray = { };
            _fileUtility
                .GetFiles(Arg.Any<string>(), Arg.Any<SearchOption>(), Arg.Any<string>())
                .Returns(x => {
                    switch (i)
                    {
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                            return fileArray;
                        case 5:
                            return noFileArray;
                        default:
                            Assert.Fail("Reached unexpected case 6 in switch statement");
                            break;
                    }
                    throw new Exception("Unexpected location reached");
                })
                .AndDoes((ci) => { i++; });
            _fileUtility
                .When(x => x.MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
                .Do((ci) =>
                {
                    if (i < 4) throw new Exception();
                });
                

            // execute
            _autoUpdaterCleanupJob.RetryTime = 1;
            _autoUpdaterCleanupJob.CleanUpTempFiles(null, new DoWorkEventArgs(null));

            // assert
            _fileUtility.Received(5).GetFiles(_autoUpdaterCleanupJob.AutoUpdaterPath, SearchOption.AllDirectories, "*.new");
            _fileUtility.Received(4).MoveFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
            _fileUtility.Received(4).MoveFile(file1, file1.Replace(".new", ""), true);
        }

        #endregion
    }
}
