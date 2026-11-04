using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.Shared
{
    internal class Constants
    {
        public const string StoreTypeMongoDb = "M";
        public const string StoreTypeRavenDb = "R";

        /// <summary>
        /// The database used by the client and server examples.
        /// </summary>
        public const string ExampleDbName = "StatelessWorkflowExample";

        public const string RootPath = "C:\\Temp";

    }
}
