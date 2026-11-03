using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;

namespace Benchmark.Agent
{
    /// <summary>
    /// A single benchmark node, compiled once per engine version.  The runner talks to it over stdin/stdout:
    ///   agent prints "READY" once connected
    ///   runner sends "SEED n" (to one node only) - agent registers n workflows and prints "SEEDED ms"
    ///   runner sends "GO" - agent executes workflows until none are left, prints "RESULT {json}" and exits
    /// Compile constants: LEGACY_EXECUTE (4.x - sync ExecuteWorkflows(count) only), MULTINODE (claim based locking).
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Dictionary<string, string> options = ParseArgs(args);
            string name = Get(options, "name", "node1");
            int concurrency = int.Parse(Get(options, "concurrency", "1"));
            int batch = int.Parse(Get(options, "batch", "50"));
            int pollMs = int.Parse(Get(options, "poll-ms", "50"));
            bool useSync = Get(options, "mode", "async") == "sync";
            TimeSpan maxTime = TimeSpan.FromMinutes(double.Parse(Get(options, "max-minutes", "30")));
            AgentSettings.WorkFolder = Get(options, "work-folder", null) ?? throw new ArgumentException("--work-folder is required");

            IMongoDatabase database = new MongoClient(Get(options, "connection", "mongodb://localhost:27017"))
                .GetDatabase(Get(options, "database", "StatelessBenchmark"));
            MongoDbWorkflowStore store = new MongoDbWorkflowStore(database);
            WorkflowServerOptions serverOptions = new WorkflowServerOptions();
#if MULTINODE
            serverOptions.NodeName = name;
            if (options.TryGetValue("claim-batch-size", out string claimBatchSize))
            {
                serverOptions.ClaimBatchSize = int.Parse(claimBatchSize);
            }
#endif
            WorkflowServer server = new WorkflowServer(store, serverOptions);
            WorkflowClient client = new WorkflowClient(store);

            // registers the type with the Mongo serializer - without it, nodes that didn't seed can't load the workflows
            server.RegisterWorkflowType<BenchmarkWorkflow>();

            int suspended = 0;
            server.WorkflowSuspended += (sender, e) => Interlocked.Increment(ref suspended);

            // warm up the connection before reporting ready
            client.GetIncompleteCount();
            Console.WriteLine("READY");

            string line;
            while ((line = Console.ReadLine()) != null && line != "GO")
            {
                if (line.StartsWith("SEED "))
                {
                    int count = int.Parse(line.Substring(5));
                    Stopwatch seedWatch = Stopwatch.StartNew();
                    for (int i = 0; i < count; i++)
                    {
                        client.Register(new BenchmarkWorkflow());
                    }
                    Console.WriteLine("SEEDED " + seedWatch.ElapsedMilliseconds);
                }
            }
            if (line == null)
            {
                Console.Error.WriteLine("stdin closed before GO");
                return 2;
            }

            Stopwatch watch = Stopwatch.StartNew();
            int executed = 0;
            int calls = 0;
            bool timedOut = false;
            while (true)
            {
                if (watch.Elapsed > maxTime)
                {
                    timedOut = true;
                    break;
                }

#if LEGACY_EXECUTE
                // 4.x has no maxConcurrent or async - it executes the loaded batch in parallel
                int count = server.ExecuteWorkflows(concurrency);
#else
                int count = useSync ? server.ExecuteWorkflows(batch, concurrency) : await server.ExecuteWorkflowsAsync(batch, concurrency);
#endif
                calls++;
                executed += count;
                if (count == 0)
                {
                    // other nodes may still be executing workflows - only stop once nothing is left to run
                    if (client.GetIncompleteCount() - client.GetSuspendedCount() <= 0)
                    {
                        break;
                    }
                    await Task.Delay(pollMs);
                }
            }
            watch.Stop();

            (server as IDisposable)?.Dispose();

            var result = new
            {
                Name = name,
                Executed = executed,
                Calls = calls,
                Suspended = suspended,
                ElapsedMs = watch.Elapsed.TotalMilliseconds,
                TimedOut = timedOut
            };
            Console.WriteLine("RESULT " + JsonSerializer.Serialize(result));
            return timedOut ? 1 : 0;
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length - 1; i += 2)
            {
                options[args[i].TrimStart('-')] = args[i + 1];
            }
            return options;
        }

        private static string Get(Dictionary<string, string> options, string key, string defaultValue)
        {
            return options.TryGetValue(key, out string value) ? value : defaultValue;
        }
    }

    public static class AgentSettings
    {
        public static string WorkFolder { get; set; }
    }
}
