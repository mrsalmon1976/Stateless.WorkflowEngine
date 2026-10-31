using Microsoft.Extensions.Hosting.WindowsServices;
using NLog.Web;
using Stateless.WorkflowEngine.WebConsole;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // when running as a Windows service the working directory is System32 - use the install folder instead
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null
});

builder.Host.UseWindowsService(o => o.ServiceName = "Stateless.WorkflowEngine.Console");
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

public partial class Program
{
}
