using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Update
{
    public interface IUpdateLocationService
    {
        string BaseFolder { get; set; }
    }

    public class UpdateLocationService : IUpdateLocationService
    {
        public UpdateLocationService()
        {
            this.BaseFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public string BaseFolder { get; set; }
    }
}
