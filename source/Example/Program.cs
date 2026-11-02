using Example;
using Example.Client;
using Example.MultiNode;
using Example.Server;
using Example.Shared;
using Example.Volume;

// command line: Example multinode reset|seed|node|report --store mongo|raven [options] (see MultiNodeExample)
if (args.Length > 0 && args[0].Equals("multinode", StringComparison.OrdinalIgnoreCase))
{
    return MultiNodeExample.Run(args.Skip(1).ToArray());
}

Prompts.WriteHeader("Stateless.WorkflowEngine examples", $"MongoDb: {AppSettings.MongoDbConnectionString}   RavenDb: {AppSettings.RavenDbUrl}");

while (true)
{
    string choice = Prompts.Choose("What would you like to do?", "0",
        ("1", "Register workflows with a WorkflowClient"),
        ("2", "Execute workflows with a WorkflowServer"),
        ("3", "High volume async client/server test (creates a SQLite database)"),
        ("4", "High volume synchronous client/server test (creates a SQLite database)"),
        ("5", "Multi-node stress test (seed, run nodes, report)"),
        ("0", "Exit"));

    if (choice == "0")
        break;

    try
    {
        switch (choice)
        {
            case "1":
                ClientExample.Run();
                break;
            case "2":
                ServerExample.Run();
                break;
            case "3":
                VolumeExample.Run(ExampleDbType.Sqlite, true);
                break;
            case "4":
                VolumeExample.Run(ExampleDbType.Sqlite, false);
                break;
            case "5":
                MultiNodeExample.RunInteractive();
                break;
        }
    }
    catch (Exception ex)
    {
        // e.g. a database isn't running - report it and go back to the menu rather than ending the app
        ConsoleWriter.WriteLine($"Error: {ex.Message}", ConsoleColor.Red);
        ConsoleWriter.WriteLine("Check that MongoDb and RavenDb are running (start.ps1).", ConsoleColor.Red);
    }
}

return 0;
