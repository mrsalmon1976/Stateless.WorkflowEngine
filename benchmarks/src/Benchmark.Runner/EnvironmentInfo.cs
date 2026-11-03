using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Benchmark.Runner
{
    /// <summary>
    /// Details of the machine, repository and database the benchmarks ran on.
    /// </summary>
    public class EnvironmentInfo
    {
        public string Cpu { get; init; } = "";
        public int LogicalCores { get; init; }
        public string Os { get; init; } = "";
        public string Runtime { get; init; } = "";
        public string GitBranch { get; init; } = "";
        public string GitCommit { get; init; } = "";
        public bool GitDirty { get; init; }
        public string MongoVersion { get; init; } = "";
        public string MongoServer { get; init; } = "";
        public string NodeHost { get; init; } = "";

        public static async Task<EnvironmentInfo> Collect(RunSettings settings, MongoClient client)
        {
            BsonDocument buildInfo = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("buildInfo", 1));
            MongoUrl url = new(settings.ConnectionString);

            return new EnvironmentInfo
            {
                Cpu = GetCpuName(),
                LogicalCores = Environment.ProcessorCount,
                Os = RuntimeInformation.OSDescription,
                Runtime = RuntimeInformation.FrameworkDescription,
                GitBranch = Git(settings.RepoRoot, "rev-parse --abbrev-ref HEAD"),
                GitCommit = Git(settings.RepoRoot, "log -1 \"--format=%h %s\""),
                GitDirty = Git(settings.RepoRoot, "status --porcelain").Length > 0,
                MongoVersion = buildInfo.GetValue("version", "unknown").ToString()!,
                // host only - never the credentials
                MongoServer = settings.UseDocker ? $"container {settings.DockerMongoContainer}" : string.Join(", ", url.Servers.Select(s => s.ToString())),
                NodeHost = settings.UseDocker
                    ? $"Docker containers (`{settings.DockerImage}`) on network `{settings.DockerNetwork}` - {Docker.Info()}"
                    : "Host processes"
            };
        }

        private static string GetCpuName()
        {
            if (OperatingSystem.IsWindows())
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string name)
                {
                    return name.Trim();
                }
            }
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        private static string Git(string repoRoot, string arguments)
        {
            try
            {
                ProcessStartInfo psi = new("git", $"-C \"{repoRoot}\" {arguments}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using Process process = Process.Start(psi)!;
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }
}
