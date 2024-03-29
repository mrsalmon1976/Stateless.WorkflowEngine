﻿using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    [TestFixture]
    public class UpdateLocationServiceTest
    {
        private string _appFolderPath = "";
        private IUpdateLocationService _updateLocationService;

        [SetUp]
        public void SetUp_UpdateLocationServiceTest()
        {
            _appFolderPath = AppContext.BaseDirectory;

            _updateLocationService = new UpdateLocationService();
            _updateLocationService.ApplicationFolder = _appFolderPath;
        }

        [TearDown]
        public void TearDown_UpdateLocationServiceTest()
        {
            if (Directory.Exists(_updateLocationService.UpdateTempFolder))
            {
                Directory.Delete(_updateLocationService.UpdateTempFolder, true);
            }
        }

        [Test]
        public void EnsureEmptyUpdateTempFolderExists_FolderDoesNotExist_CreatesFolder()
        {
            Assert.IsFalse(Directory.Exists(_updateLocationService.UpdateTempFolder));
            _updateLocationService.EnsureEmptyUpdateTempFolderExists();
            Assert.IsTrue(Directory.Exists(_updateLocationService.UpdateTempFolder));
        }

        [Test]
        public void EnsureEmptyUpdateTempFolderExists_FolderAlreadyExistsEmpty_CreatesEmptyFolder()
        {
            Directory.CreateDirectory(_updateLocationService.UpdateTempFolder);
            _updateLocationService.EnsureEmptyUpdateTempFolderExists();
            Assert.IsTrue(Directory.Exists(_updateLocationService.UpdateTempFolder));

        }

        [Test]
        public void EnsureEmptyUpdateTempFolderExists_FolderAlreadyExistsWithFiles_CreatesEmptyFolder()
        {
            string filePath = Path.Combine(_updateLocationService.UpdateTempFolder, "myfile.txt");
            Directory.CreateDirectory(_updateLocationService.UpdateTempFolder);
            File.WriteAllText(filePath, "this is a test");
            Assert.IsTrue(File.Exists(filePath));


            _updateLocationService.EnsureEmptyUpdateTempFolderExists();
            
            Assert.IsTrue(Directory.Exists(_updateLocationService.UpdateTempFolder));
            Assert.IsFalse(File.Exists(filePath));

        }

    }
}
