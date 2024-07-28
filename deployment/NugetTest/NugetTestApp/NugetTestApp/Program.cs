using Example.Volume;
using NugetTestApp;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using System.Data;

// determine which workflow store to use
ConsoleWriter.WriteLine(@"Which workflow store would you like to check?
    Memory [1]
    MongoDb [2]
    RavenDb [3]");
ConsoleWriter.Write(":");
string? storeType = Console.ReadLine();

TestAppRunner.Run(storeType);

Console.Read();




