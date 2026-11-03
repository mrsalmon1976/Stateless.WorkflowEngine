namespace Benchmark.Runner
{
    /// <summary>
    /// A version of the engine being benchmarked - either a release zip or the current branch.
    /// </summary>
    public class EngineVersion
    {
        public const string CurrentName = "current";

        public required string Name { get; init; }

        public Version? Version { get; init; }

        public bool IsCurrent => this.Name == CurrentName;

        /// <summary>
        /// Versions before 5.0 only have <c>ExecuteWorkflows(count)</c>: no maxConcurrent and no async.
        /// </summary>
        public bool IsLegacyExecute => !this.IsCurrent && this.Version!.Major < 5;

        /// <summary>
        /// Claim based multi-node locking arrived in 6.0.
        /// </summary>
        public bool SupportsMultiNode => this.IsCurrent || this.Version!.Major >= 6;

        public string ExecutionApi(string mode) => this.IsLegacyExecute ? "ExecuteWorkflows(count) - sync"
            : mode == "sync" ? "ExecuteWorkflows(count, maxConcurrent)" : "ExecuteWorkflowsAsync(count, maxConcurrent)";

        /// <summary>
        /// Package versions the release was built against (from its deps.json), keyed by package id.
        /// </summary>
        public Dictionary<string, string> Dependencies { get; init; } = new();

        public string AgentDll { get; set; } = "";

        public bool Supports(Scenario scenario)
        {
            return scenario.Nodes == 1 || this.SupportsMultiNode;
        }
    }

    public record Scenario(int Number, int Concurrency, int Nodes)
    {
        public int TotalConcurrency => this.Concurrency * this.Nodes;

        public string Title => $"Concurrency {this.Concurrency}, {this.Nodes} node{(this.Nodes == 1 ? "" : "s")}";

        public string ShortTitle => $"{this.Nodes}×{this.Concurrency}";

        /// <summary>
        /// The scenarios, grouped by total concurrency (nodes × concurrency) - 1×4 and 2×2 compare one node with two
        /// nodes running the same number of workflows at once.
        /// </summary>
        public static readonly Scenario[] All =
        {
            new(1, 1, 1),
            new(2, 4, 1),
            new(3, 2, 2),
            new(4, 10, 1),
            new(5, 10, 2),
            new(6, 10, 5)
        };
    }

    public class Stats
    {
        public double Mean { get; init; }
        public double P50 { get; init; }
        public double P95 { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }

        public static Stats From(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
            {
                return new Stats();
            }
            List<double> sorted = values.OrderBy(v => v).ToList();
            return new Stats
            {
                Mean = sorted.Average(),
                P50 = Percentile(sorted, 0.50),
                P95 = Percentile(sorted, 0.95),
                Min = sorted[0],
                Max = sorted[^1]
            };
        }

        private static double Percentile(List<double> sorted, double p)
        {
            double rank = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(rank);
            int upper = (int)Math.Ceiling(rank);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
        }
    }

    public class NodeResult
    {
        public string Name { get; set; } = "";
        public int Executed { get; set; }
        public int Calls { get; set; }
        public int Suspended { get; set; }
        public double ElapsedMs { get; set; }
        public bool TimedOut { get; set; }
    }

    public class IterationResult
    {
        public int Iteration { get; init; }
        public double TotalMs { get; set; }

        /// <summary>
        /// Time to complete the workflows: from the first workflow starting to the last one completing.
        /// </summary>
        public double RunElapsedMs { get; set; }
        public double SeedMs { get; set; }
        public long Completed { get; set; }
        public long Active { get; set; }
        public long Suspended { get; set; }
        public int FilesLeft { get; set; }
        public Stats Execution { get; set; } = new();
        public Stats Elapsed { get; set; } = new();
        public List<NodeResult> Nodes { get; set; } = new();
        public string? Error { get; set; }

        public bool Passed(int expected) => this.Error == null && this.Completed == expected && this.Active == 0
            && this.Suspended == 0 && this.FilesLeft == 0 && this.Nodes.All(n => !n.TimedOut);

        public double Throughput => this.TotalMs > 0 ? this.Completed / (this.TotalMs / 1000) : 0;
    }

    public class ScenarioResult
    {
        public required string Version { get; init; }
        public required Scenario Scenario { get; init; }
        public List<IterationResult> Iterations { get; init; } = new();

        public bool AllPassed(int expected) => this.Iterations.Count > 0 && this.Iterations.All(i => i.Passed(expected));

        public double Median(Func<IterationResult, double> selector)
        {
            List<double> values = this.Iterations.Select(selector).OrderBy(v => v).ToList();
            if (values.Count == 0)
            {
                return 0;
            }
            int mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
        }
    }

    public class RunSettings
    {
        public string BenchmarksRoot { get; set; } = "";
        public string RepoRoot => Path.GetFullPath(Path.Combine(this.BenchmarksRoot, ".."));
        public string WorkRoot => Path.Combine(this.BenchmarksRoot, ".work");
        public string ConnectionString { get; set; } = Environment.GetEnvironmentVariable("STATELESS_MONGODB_CONNECTIONSTRING") ?? "mongodb://localhost:27017";
        public string Database { get; set; } = "StatelessBenchmark";
        public int Count { get; set; } = 1000;
        public int Iterations { get; set; } = 3;
        public int Batch { get; set; } = 50;
        public int PollMs { get; set; } = 50;

        /// <summary>
        /// "async" (ExecuteWorkflowsAsync) or "sync" (ExecuteWorkflows) - versions before 5.0 are always sync.
        /// </summary>
        public string Mode { get; set; } = "async";

        /// <summary>
        /// WorkflowServerOptions.ClaimBatchSize for versions that claim workflows - null uses the version default.
        /// </summary>
        public int? ClaimBatchSize { get; set; }

        /// <summary>
        /// The folder the report is written to - defaults to benchmarks/reports.
        /// </summary>
        public string? ReportsFolder { get; set; }
        public int MaxMinutes { get; set; } = 15;
        public double RegressionThreshold { get; set; } = 10;
        public bool SkipBuild { get; set; }

        /// <summary>
        /// Where the nodes run: "docker" (a container on the MongoDB container's network) or "local" (host processes).
        /// Docker Desktop stalls host-to-container requests over ~750 bytes by 40ms, so docker is the default.
        /// </summary>
        public string Host { get; set; } = "docker";
        public bool UseDocker => this.Host == "docker";
        public string DockerImage { get; set; } = "mcr.microsoft.com/dotnet/runtime:10.0";
        public string DockerMongoContainer { get; set; } = "stateless_mongodb";
        public string DockerVolume { get; set; } = "stateless-benchmark-work";
        public string DockerNetwork { get; set; } = "";
        public string AgentConnectionString => this.UseDocker ? $"mongodb://{this.DockerMongoContainer}:27017" : this.ConnectionString;
        public HashSet<string>? VersionFilter { get; set; }
        public HashSet<int>? ScenarioFilter { get; set; }
    }
}
