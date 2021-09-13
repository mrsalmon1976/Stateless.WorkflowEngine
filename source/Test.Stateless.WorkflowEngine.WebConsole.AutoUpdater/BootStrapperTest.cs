using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    [TestFixture]
    public class BootStrapperTest
    {
        [Test]
        public void Boot_Execute()
        {
            BootStrapper.Boot();
        }
    }
}
