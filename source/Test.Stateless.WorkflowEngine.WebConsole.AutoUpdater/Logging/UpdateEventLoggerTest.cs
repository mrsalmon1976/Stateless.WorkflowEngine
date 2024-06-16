using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging
{
    [TestFixture]
    public class UpdateEventLoggerTest
    {
        private string _eventLogPath = "";

        private IUpdateEventLogger _updateEventLogger;

        private IUpdateLocationService _updateLocationService;

        [SetUp]
        public void SetUp_UpdateEventLoggerTest()
        {

            _eventLogPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestLog.log");
            File.Delete(_eventLogPath);

            _updateLocationService = Substitute.For<IUpdateLocationService>();
            _updateLocationService.UpdateEventLogFilePath.Returns(_eventLogPath);

            _updateEventLogger = new UpdateEventLogger(_updateLocationService);
        }

        [TearDown]
        public void TearDown_UpdateEventLoggerTest()
        {

            File.Delete(_eventLogPath);
        }


        [Test]
        public void ClearLogFile_OnExecute_ClearsOutFile()
        {
            // set up
            File.WriteAllText(_eventLogPath, "this is a test");

            // execute
            _updateEventLogger.ClearLogFile();

            // assert
            string contents = File.ReadAllText(_eventLogPath);
            Assert.That(contents, Is.EqualTo(String.Empty));
        }

        [Test]
        public void Log_OnExecute_LogsWithoutLineBreaks()
        {
            // execute
            _updateEventLogger.Log("a");
            _updateEventLogger.Log("b");
            _updateEventLogger.Log("c");

            // assert
            string contents = File.ReadAllText(_eventLogPath);
            Assert.That(contents, Is.EqualTo("abc"));
        }

        [Test]
        public void LogLine_OnExecute_LogsWithLineBreaks()
        {
            // execute
            _updateEventLogger.LogLine("a");
            _updateEventLogger.LogLine("b");
            _updateEventLogger.LogLine("c");
                                  
            // assert
            string contents = File.ReadAllText(_eventLogPath);
            string expected = String.Format("a{0}b{0}c{0}", Environment.NewLine);
            Assert.That(contents, Is.EqualTo(expected));
        }

        [Test]
        public void LogLine_WithLog_LogsWithAndWithoutLineBreaks()
        {
            // execute
            _updateEventLogger.Log("a");
            _updateEventLogger.LogLine("b");
            _updateEventLogger.Log("c");
            _updateEventLogger.LogLine("d");

            // assert
            string contents = File.ReadAllText(_eventLogPath);
            string expected = String.Format("ab{0}cd{0}", Environment.NewLine);
            Assert.That(contents, Is.EqualTo(expected));
        }

    }
}
