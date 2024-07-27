using Example.Client;
using Example.Server;
using Example.Shared;
using Example.Volume;
using SharpCompress.Common;
using System.Data.SqlTypes;

Console.WriteLine(@"Would you like to: 
    1. register workflows with a WorkflowClient [1] 
    2. execute workflows with a WorkflowServer [2] ?
    3. execute a high volume client/server test (creates a SQLite database) [3]
    4. execute a high volume client/server test (requires a SqlServer database) [4]");
string? input = Console.ReadLine()?.ToUpper();
Console.WriteLine("");

if (input == "1")
{
    ClientExample.Run();
}
else if (input == "2")
{
    ServerExample.Run();
}
else if (input == "3")
{
    VolumeExample.Run(ExampleDbType.Sqlite);
}
else if (input == "4")
{
    VolumeExample.Run(ExampleDbType.SqlServer);
}
else
{
    Console.WriteLine("Invalid input option....exiting. Hit enter to continue.");
    Console.ReadLine();
}