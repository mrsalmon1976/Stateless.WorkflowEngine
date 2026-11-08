using Microsoft.Extensions.Hosting.WindowsServices;
using NLog.Web;
using Stateless.WorkflowEngine.WebConsole;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

// the 5.x Update.ps1 runs "<exe> install" and "<exe> start" (Topshelf commands) and waits for them to exit - without
// this the console would ignore the command, run in a console window and hang the old script
string[] legacyServiceCommands = ["install", "uninstall", "start", "stop"];
if (args.Length > 0 && legacyServiceCommands.Contains(args[0], StringComparer.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"'{args[0]}' is not supported - the console is now a standard Windows service.  Run Update.ps1 or install-service.ps1 from the new release to install or convert the service.");
    return 1;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // when running as a Windows service the working directory is System32 - use the install folder instead
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null
});

builder.Services.AddWindowsService(o => o.ServiceName = "Stateless.WorkflowEngine.Console");
builder.Logging.ClearProviders();
builder.Host.UseNLog();

var appSettings = new AppSettings(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddWebConsole(appSettings);
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// unknown api routes should 404 rather than fall through to the SPA
app.Map("/api/{**path}", (HttpContext ctx) => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
return 0;

public partial class Program
{
}
