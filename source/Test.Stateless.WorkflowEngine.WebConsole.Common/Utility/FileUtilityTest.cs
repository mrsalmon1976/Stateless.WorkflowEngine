using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Common.Utility
{
    [TestFixture]
    public class FileUtilityTest
    {
        private string _testFolder = "";

        private IFileUtility _fileUtility;

        [SetUp]
        public void SetUp_FileUtilityTest()
        {
            _testFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FileUtilityTest");
            if (Directory.Exists(_testFolder))
            {
                Directory.Delete(_testFolder, true);
            }

            _fileUtility = new FileUtility();

            Directory.CreateDirectory(_testFolder);
        }

        [TearDown]
        public void TearDown_FileUtilityTest()
        {
            if (Directory.Exists(_testFolder))
            {
                Directory.Delete(_testFolder, true);
            }

        }

        #region CopyRecursive tests

        [Test]
        public void CopyRecursive_WithDirectoryInfoArguments_CopiesRootFiles()
        {
            // Set up
            string[] files = { "test1.txt", "text2.txt" };
            string sourceFolder = Path.Combine(_testFolder, "DI_Source");
            CreateFiles(sourceFolder, files);
            AssertFilesExist(sourceFolder, files);

            string destinationFolder = Path.Combine(_testFolder, "DI_Destination");
            Directory.CreateDirectory(destinationFolder);

            // execute
            _fileUtility.CopyRecursive(new DirectoryInfo(sourceFolder), new DirectoryInfo(destinationFolder));

            // assert files exist
            AssertFilesExist(destinationFolder, files);
        }

        [Test]
        public void CopyRecursive_WithDirectoryInfoArguments_CopiesDeepFolders()
        {
            // Set up
            string[] files = { "Folder1\\test1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\test1_1_1.txt", "Folder2\\Folder2_2\\test2_2.txt" };
            string sourceFolder = Path.Combine(_testFolder, "DI_Source");
            CreateFiles(sourceFolder, files);
            AssertFilesExist(sourceFolder, files);

            string destinationFolder = Path.Combine(_testFolder, "DI_Destination");
            Directory.CreateDirectory(destinationFolder);

            // execute
            _fileUtility.CopyRecursive(new DirectoryInfo(sourceFolder), new DirectoryInfo(destinationFolder));

            // assert files exist
            AssertFilesExist(destinationFolder, files);
        }

        [Test]
        public void CopyRecursive_WithStringArguments_CopiesRootFiles()
        {
            // Set up
            string[] files = { "test1.txt", "text2.txt" };
            string sourceFolder = Path.Combine(_testFolder, "Source");
            CreateFiles(sourceFolder, files);
            AssertFilesExist(sourceFolder, files);

            string destinationFolder = Path.Combine(_testFolder, "Destination");
            Directory.CreateDirectory(destinationFolder);

            // execute
            _fileUtility.CopyRecursive(sourceFolder, destinationFolder);

            // assert files exist
            AssertFilesExist(destinationFolder, files);
        }

        [Test]
        public void CopyRecursive_WithStringArguments_CopiesDeepFolders()
        {
            // Set up
            string[] files = { "Folder1\\test1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\test1_1_1.txt", "Folder2\\Folder2_2\\test2_2.txt" };
            string sourceFolder = Path.Combine(_testFolder, "Source");
            CreateFiles(sourceFolder, files);
            AssertFilesExist(sourceFolder, files);

            string destinationFolder = Path.Combine(_testFolder, "Destination");
            Directory.CreateDirectory(destinationFolder);

            // execute
            _fileUtility.CopyRecursive(sourceFolder, destinationFolder);

            // assert files exist
            AssertFilesExist(destinationFolder, files);
        }

        [Test]
        public void CopyRecursive_WithFileExclusions_DoesNotCopyExcludedFiles()
        {
            // Set up
            string[] files = { "Folder1\\test1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\test1_1_1.txt", "Folder2\\Folder2_2\\test2_2.txt" };
            string[] excludedfiles = { "Folder1\\extest1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\extest1_1_1.txt", "Folder2\\Folder2_2\\extest2_2.txt" };
            List<string> allFiles = new List<string>(files);
            allFiles.AddRange(excludedfiles);

            string sourceFolder = Path.Combine(_testFolder, "Source");
            CreateFiles(sourceFolder, allFiles);
            AssertFilesExist(sourceFolder, allFiles);

            string destinationFolder = Path.Combine(_testFolder, "Destination");
            Directory.CreateDirectory(destinationFolder);

            // execute
            IEnumerable<string> excludedFilesWithFullPath = excludedfiles.Select(x => Path.Combine(sourceFolder, x));
            _fileUtility.CopyRecursive(sourceFolder, destinationFolder, excludedFilesWithFullPath);

            // assert files exist
            AssertFilesExist(destinationFolder, files);
            AssertFilesDoNotExist(destinationFolder, excludedfiles);
        }

        [Test]
        public void CopyRecursive_WithFileExclusions_DoesNotCopyExcludedFolders()
        {
            // Set up
            string[] files = { "Folder1\\test1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\test1_1_1.txt", "Folder2\\Folder2_2\\test2_2.txt" };
            string[] excludedfiles = { "Folder2\\extest2.txt", "Folder2\\Folder2_2\\extest2_2.txt", "Folder3\\extest3.txt" };
            List<string> allFiles = new List<string>(files);
            allFiles.AddRange(excludedfiles);

            string sourceFolder = Path.Combine(_testFolder, "Source");
            CreateFiles(sourceFolder, allFiles);
            AssertFilesExist(sourceFolder, allFiles);

            string destinationFolder = Path.Combine(_testFolder, "Destination");
            Directory.CreateDirectory(destinationFolder);

            // execute
            IEnumerable<string> excludedFilesWithFullPath = excludedfiles.Select(x => Path.Combine(sourceFolder, x));
            _fileUtility.CopyRecursive(sourceFolder, destinationFolder, excludedFilesWithFullPath);

            // assert files exist
            AssertFilesExist(destinationFolder, files);
            AssertFilesDoNotExist(destinationFolder, excludedfiles);
        }

        #endregion

        #region DeleteContents tests

        [Test]
        public void DeleteContents_WithStringArguments_DeletesAllSubFolders()
        {
            // Set up
            string[] files = { "Folder1\\test1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\test1_1_1.txt", "Folder2\\Folder2_2\\test2_2.txt" };
            string sourceFolder = Path.Combine(_testFolder, "DeleteSource");
            CreateFiles(sourceFolder, files);
            AssertFilesExist(sourceFolder, files);

            // execute
            _fileUtility.DeleteContents(sourceFolder);

            // assert 
            DirectoryInfo di = new DirectoryInfo(sourceFolder);
            Assert.That(di.GetDirectories().Count(), Is.EqualTo(0));
            Assert.That(di.GetFiles().Count(), Is.EqualTo(0));
        }

        [Test]
        public void DeleteContents_WithExclusions_DeletesAllSubFoldersExceptForExcluded()
        {
            // Set up
            string[] files = { "deleted.txt", "Folder1\\test1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\test1_1_1.txt" };
            string[] excludedFiles = { "kept.txt", "Folder2\\RetainedFolder\\test.txt", "Folder3\\test2.txt" };
            IEnumerable<string> excludedArgs = new string[] { "kept.txt", "Folder2", "Folder3" };
            List<string> allFiles = new List<string>(files);
            allFiles.AddRange(excludedFiles);


            string sourceFolder = Path.Combine(_testFolder, "DeleteSource");
            CreateFiles(sourceFolder, allFiles);
            AssertFilesExist(sourceFolder, allFiles);

            // execute
            excludedArgs = excludedArgs.Select(x => Path.Combine(sourceFolder, x));
            _fileUtility.DeleteContents(sourceFolder, excludedArgs);

            // assert 
            AssertFilesExist(sourceFolder, excludedFiles);
            AssertFilesDoNotExist(sourceFolder, files);
        }

        #endregion

        #region DeleteDirectoryRecursive

        [Test]
        public void DeleteDirectoryRecursive_OnExecute_DeletesFolderAndSubfolders()
        {
            // Set up
            string[] files = { "deleted.txt", "Folder1\\test1.txt", "Folder1\\Folder1_1\\Folder1_1_1\\test1_1_1.txt" };

            string sourceFolder = Path.Combine(_testFolder, "DeleteDirectoryRecursive");
            CreateFiles(sourceFolder, files);
            AssertFilesExist(sourceFolder, files);

            // execute
            _fileUtility.DeleteDirectoryRecursive(sourceFolder);

            // assert 
            Assert.That(Directory.Exists(sourceFolder), Is.False);
        }

        #endregion

        #region Helper functions

        private void CreateFiles(string rootFolder, IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                string filePath = Path.Combine(rootFolder, file);
                string directoryPath = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(directoryPath);
                File.WriteAllText(filePath, Guid.NewGuid().ToString());
            }
        }

        private void AssertFilesExist(string rootFolder, IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                string filePath = Path.Combine(rootFolder, file);
                if (!File.Exists(filePath))
                {
                    Assert.Fail($"File {filePath} was expected but does not exist");
                }

            }
        }

        private void AssertFilesDoNotExist(string rootFolder, IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                string filePath = Path.Combine(rootFolder, file);
                if (File.Exists(filePath))
                {
                    Assert.Fail($"File {filePath} was not expected to exist but is present");
                }

            }
        }

        #endregion


    }
}
