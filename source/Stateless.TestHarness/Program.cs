// NOTE: This is a TestHarness project for trying out integration / long-running aspects 
// of the solution.  This assembly is not intended to be used in any way, it is purely 
// a test bed for scenarios

using Stateless.TestHarness;

Console.WriteLine("What would you like to do?");
Console.WriteLine("  [1] Run a multithreading test");
Console.Write(" >> ");

string? choice = Console.ReadLine();

switch (choice)
{
    case "1":
        Console.WriteLine("");
        Console.WriteLine("Running multithreading test");
        MultithreadTestHarness.Run();
        break;
    default:
        Console.WriteLine("Invalid choice - not doing anything");
        break;
}

Console.Read();

