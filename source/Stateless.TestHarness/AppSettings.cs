using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.TestHarness
{
    internal class AppSettings
    {
        public const string ConnectionString = "Server=localhost;Database=StatelessTest;User Id=dev;Password=dev;TrustServerCertificate=True";

        public const string MongoDbDatabaseName = "StatelessTestHarness";
    }
}
