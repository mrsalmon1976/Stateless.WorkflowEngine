using Example.Client;
using Example.Server;

Console.WriteLine(@"Would you like to: 
    1. register workflows with the client [C] 
    2. execute workflows with the server [S] ?");
string? input = Console.ReadLine()?.ToUpper();
Console.WriteLine("");

if (input == "C")
{
    ClientExample example = new ClientExample();
    example.Run();
}
else if (input == "S")
{
    ServerExample example = new ServerExample();
    example.Run();
}
else 
{
    Console.WriteLine("Invalid input option....exiting. Hit enter to continue.");
    Console.ReadLine();
}