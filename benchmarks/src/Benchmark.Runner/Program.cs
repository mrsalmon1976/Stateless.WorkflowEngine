using System.Diagnostics;
using System.Globalization;
using MongoDB.Driver;

namespace Benchmark.Runner
{
    /// <summary>
    /// Benchmarks every release in benchmarks/releases against the current branch, and writes
    /// benchmarks/reports/benchmark-YYYYMMDD-HHMM.md.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            RunSettings settings;
            try
            {
                settings = ParseArgs(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 2;
            }

            DateTime startedAt = DateTime.Now;
            Stopwatch runWatch = Stopwatch.StartNew();
            MongoClient client = new(settings.ConnectionString);
            if (settings.UseDocker)
            {
                Docker.Prepare(settings);
            }
            EnvironmentInfo environment = await EnvironmentInfo.Collect(settings, client);

            AgentBuilder builder = new(settings);
            List<EngineVersion> versions = builder.DiscoverVersions();
            foreach (EngineVersion version in versions)
            {
                builder.Build(version);
            }

            List<Scenario> scenarios = Scenario.All.Where(s => settings.ScenarioFilter == null || settings.ScenarioFilter.Contains(s.Number)).ToList();
            ScenarioRunner runner = new(settings, client);
            List<ScenarioResult> results = new();
            foreach (EngineVersion version in versions)
            {
                foreach (Scenario scenario in scenarios.Where(version.Supports))
                {
                    ScenarioResult result = new() { Version = version.Name, Scenario = scenario };
                    results.Add(result);
                    for (int i = 1; i <= settings.Iterations; i++)
                    {
                        Console.Write($"{version.Name,-8} scenario {scenario.Number} ({scenario.Title}) run {i}/{settings.Iterations}... ");
                        IterationResult iteration = await runner.Run(version, scenario, i);
                        result.Iterations.Add(iteration);
                        Console.WriteLine($"{iteration.TotalMs / 1000:0.00}s, avg execution {iteration.Execution.Mean:0.0}ms"
                            + (iteration.Passed(settings.Count) ? "" : $"  FAILED: {iteration.Error ?? $"{iteration.Completed} completed, {iteration.Active} active, {iteration.FilesLeft} files left"}"));
                    }
                }
            }
            // write the report before cleaning up, so a clean up failure can't lose the results
            ReportWriter writer = new(settings, environment, versions, results, startedAt, runWatch.Elapsed);
            string reportPath = writer.Write();
            Console.WriteLine();
            Console.WriteLine($"Report written to {reportPath}");

            await client.DropDatabaseAsync(settings.Database);
            if (settings.UseDocker)
            {
                Docker.CleanUp(settings);
            }
            return results.All(r => r.AllPassed(settings.Count)) ? 0 : 1;
        }

        private static RunSettings ParseArgs(string[] args)
        {
            RunSettings settings = new();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{arg} needs a value");
                switch (arg)
                {
                    case "--root": settings.BenchmarksRoot = Path.GetFullPath(Next()); break;
                    case "--connection": settings.ConnectionString = Next(); break;
                    case "--database": settings.Database = Next(); break;
                    case "--count": settings.Count = int.Parse(Next()); break;
                    case "--iterations": settings.Iterations = int.Parse(Next()); break;
                    case "--batch": settings.Batch = int.Parse(Next()); break;
                    case "--poll-ms": settings.PollMs = int.Parse(Next()); break;
                    case "--mode": settings.Mode = Next() is "sync" or "async" ? args[i] : throw new ArgumentException("--mode must be sync or async"); break;
                    case "--reports": settings.ReportsFolder = Path.GetFullPath(Next()); break;
                    case "--claim-batch-size": settings.ClaimBatchSize = int.Parse(Next()); break;
                    case "--max-minutes": settings.MaxMinutes = int.Parse(Next()); break;
                    case "--threshold": settings.RegressionThreshold = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--versions": settings.VersionFilter = Next().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase); break;
                    case "--scenarios": settings.ScenarioFilter = Next().Split(',').Select(int.Parse).ToHashSet(); break;
                    case "--skip-build": settings.SkipBuild = true; break;
                    case "--host": settings.Host = Next() is "local" or "docker" ? args[i] : throw new ArgumentException("--host must be docker or local"); break;
                    case "--docker-image": settings.DockerImage = Next(); break;
                    case "--mongo-container": settings.DockerMongoContainer = Next(); break;
                    default: throw new ArgumentException($"Unknown argument {arg}");
                }
            }

            if (string.IsNullOrEmpty(settings.BenchmarksRoot))
            {
                settings.BenchmarksRoot = FindBenchmarksRoot() ?? throw new ArgumentException("Could not find the benchmarks folder - pass --root");
            }
            return settings;
        }

        private static string? FindBenchmarksRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (DirectoryInfo? dir = new(start); dir != null; dir = dir.Parent)
                {
                    foreach (string candidate in new[] { dir.FullName, Path.Combine(dir.FullName, "benchmarks") })
                    {
                        if (Directory.Exists(Path.Combine(candidate, "releases")) && Directory.Exists(Path.Combine(candidate, "src", "Benchmark.Agent")))
                        {
                            return candidate;
                        }
                    }
                }
            }
            return null;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("""
                Usage: Benchmark.Runner [options]
                  --root <path>          benchmarks folder (found automatically)
                  --connection <uri>     MongoDB connection (default STATELESS_MONGODB_CONNECTIONSTRING or mongodb://localhost:27017)
                  --database <name>      database used for the runs - it is dropped (default StatelessBenchmark)
                  --count <n>            workflows per run (default 1000)
                  --iterations <n>       runs per version and scenario (default 3)
                  --batch <n>            count passed to ExecuteWorkflowsAsync (default 50)
                  --poll-ms <n>          node idle poll interval (default 50)
                  --mode <async|sync>    execute with ExecuteWorkflowsAsync (default) or ExecuteWorkflows - 4.x is always sync
                  --reports <path>       folder for the report (default benchmarks/reports)
                  --claim-batch-size <n> WorkflowServerOptions.ClaimBatchSize for versions that claim (default: version default)
                  --max-minutes <n>      timeout per run (default 15)
                  --threshold <pct>      regression threshold vs the latest release (default 10)
                  --versions <list>      e.g. 5.1.0,current
                  --scenarios <list>     e.g. 1,2
                  --skip-build           reuse agents that are already built
                  --host <docker|local>  where the nodes run (default docker - a container on the MongoDB container's network,
                                         because Docker Desktop stalls host-to-container requests over ~750 bytes by 40ms)
                  --docker-image <name>  node image (default mcr.microsoft.com/dotnet/runtime:10.0)
                  --mongo-container <n>  MongoDB container the nodes connect to (default stateless_mongodb)
                """);
        }
    }
}
