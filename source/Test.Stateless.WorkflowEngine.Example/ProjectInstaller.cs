using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.Example
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        internal static string ServiceName { get; set; }

        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}
