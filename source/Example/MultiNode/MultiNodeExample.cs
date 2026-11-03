using Example.Shared;
using Example.Shared.Workflows.MultiNode;
using MongoDB.Driver;
using Raven.Client.Documents;
using Raven.Client.ServerWide.Operations;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.RavenDb;
using Stateless.WorkflowEngine.Stores;
using System.Diagnostics;
using System.Text.Json;

namespace Example.MultiNode
{
    /// <summary>
    /// Multi-node stress test: any number of node processes execute the same workflows from one store, and the
    /// execution ledger proves that no workflow step was ever executed by two nodes at the same time.
    ///
    ///   Example multinode reset  --store mongo|raven
    ///   Example multinode seed   --store mongo|raven --count 2000
    ///   Example multinode node   --store mongo|raven --name A [--batch 200] [--concurrency 20] [--async] [--crash-after 30]
    ///                            [--error-rate 0.02] [--io-kb 256] [--node-timeout 20] [--slow-rate 0.005] [--max-minutes 30]
    ///   Example multinode report --store mongo|raven
    /// </summary>
    internal class MultiNodeExample
    {
        public const string StoreMongo = "mongo";
        public const string StoreRaven = "raven";
        private const string DbName = "StatelessMultiNode";
        private static readonly string[] Steps = { "WriteFile", "TransformFile", "Finalise" };

        public static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: Example multinode reset|seed|node|report --store mongo|raven [options]");
                return 2;
            }

            Dictionary<string, string> options = ParseOptions(args.Skip(1));
            string store = GetOption(options, "store", "").ToLowerInvariant();
            if (store != StoreMongo && store != StoreRaven)
            {
                Console.WriteLine("--store must be 'mongo' or 'raven'");
                return 2;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "reset":
                    Reset(store);
                    return 0;
                case "seed":
                    Seed(store, int.Parse(GetOption(options, "count", "2000")));
                    return 0;
                case "node":
                    return RunNodeAsync(store, options).GetAwaiter().GetResult();
                case "report":
                    return Report(store);
                default:
                    Console.WriteLine($"Unknown command '{args[0]}'");
                    return 2;
            }
        }

        /// <summary>
        /// Interactive entry point from the example menu.
        /// </summary>
        public static void RunInteractive()
        {
            string? store = ChooseStore();
            while (store != null)
            {
                Prompts.WriteHeader($"Multi-node stress test - {store}", GetStatus(store));
                string choice = Prompts.Choose("What would you like to do?", "0",
                    ("1", "Seed workflows"),
                    ("2", "Run a node in this window"),
                    ("3", "Report"),
                    ("4", "Reset the store, execution ledger and work folder"),
                    ("5", $"Switch to {(store == StoreMongo ? "RavenDb" : "MongoDb")}"),
                    ("0", "Back"));

                try
                {
                    switch (choice)
                    {
                        case "1":
                            Seed(store, Prompts.AskInt("Number of workflows", 2000));
                            break;
                        case "2":
                            RunInteractiveNode(store);
                            break;
                        case "3":
                            Report(store);
                            break;
                        case "4":
                            if (Prompts.AskYesNo($"Delete all workflows in the {store} store and the execution ledger?", false))
                            {
                                Reset(store);
                            }
                            break;
                        case "5":
                            store = (store == StoreMongo ? StoreRaven : StoreMongo);
                            continue;
                        case "0":
                            return;
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWriter.WriteLine($"Error: {ex.Message}", ConsoleColor.Red);
                }
                Prompts.Pause();
            }
        }

        private static string? ChooseStore()
        {
            string storeType = Prompts.GetInputStoreType();
            if (string.IsNullOrEmpty(storeType))
            {
                return null;
            }
            return (storeType == Constants.StoreTypeMongoDb ? StoreMongo : StoreRaven);
        }

        /// <summary>
        /// Gets a one-line summary of the store and ledger for the menu header.
        /// </summary>
        private static string GetStatus(string store)
        {
            try
            {
                IWorkflowStore workflowStore = GetStore(store);
                int seeded = new ExecutionLedger(store).GetSeededCount();
                return $"Seeded {seeded}, active {workflowStore.GetIncompleteCount()}, completed {workflowStore.GetCompletedCount()}, nodes running {workflowStore.GetNodes().Count()}";
            }
            catch (Raven.Client.Exceptions.Database.DatabaseDoesNotExistException)
            {
                return "Not seeded yet";
            }
            catch (Exception ex)
            {
                return $"Unable to read the store: {ex.Message}";
            }
        }

        private static void RunInteractiveNode(string store)
        {
            Dictionary<string, string> options = new Dictionary<string, string>
            {
                ["name"] = Prompts.Ask("Node name", Environment.MachineName),
                ["concurrency"] = Prompts.AskInt("Workflows to run at once", 20).ToString(),
                ["node-timeout"] = Prompts.AskInt("Node timeout in seconds (minimum 4)", 20, 4).ToString(),
                // the interactive node runs until every workflow has completed (or Q is pressed)
                ["max-minutes"] = int.MaxValue.ToString()
            };
            if (Prompts.AskYesNo("Use the async server methods?", true))
            {
                options["async"] = "true";
            }

            Console.WriteLine();
            ConsoleWriter.WriteLine("The node runs until every workflow has completed - press Q to stop it early.", ConsoleColor.Gray);
            ConsoleWriter.WriteLine($"Run more nodes in other windows with: dotnet {typeof(MultiNodeExample).Assembly.Location} multinode node --store {store} --name <name>", ConsoleColor.Gray);

            using (CancellationTokenSource stop = new CancellationTokenSource())
            {
                Task keyWatcher = Task.Run(() =>
                {
                    while (!stop.IsCancellationRequested)
                    {
                        if (!Console.IsInputRedirected && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                        {
                            ConsoleWriter.WriteLine("Stopping the node - waiting for the running workflows to finish...", ConsoleColor.Yellow);
                            stop.Cancel();
                        }
                        Thread.Sleep(100);
                    }
                });

                RunNodeAsync(store, options, stop.Token).GetAwaiter().GetResult();
                stop.Cancel();
                keyWatcher.Wait();
            }
        }

        private static void Reset(string store)
        {
            if (store == StoreMongo)
            {
                new MongoClient(AppSettings.MongoDbConnectionString).DropDatabase(DbName);
            }
            else
            {
                using (IDocumentStore documentStore = CreateDocumentStore())
                {
                    try
                    {
                        documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(DbName, hardDelete: true));
                    }
                    catch (Raven.Client.Exceptions.Database.DatabaseDoesNotExistException)
                    {
                        // nothing to delete
                    }
                }
            }

            new ExecutionLedger(store).Drop();

            string workFolder = MultiNodeContext.GetWorkFolder(store);
            if (Directory.Exists(workFolder))
            {
                Directory.Delete(workFolder, true);
            }
            ConsoleWriter.WriteLine($"Reset the {store} store, its execution ledger and work folder", ConsoleColor.Cyan);
        }

        private static void Seed(string store, int count)
        {
            IWorkflowStore workflowStore = GetStore(store);
            workflowStore.Initialise(true, true, false);
            workflowStore.RegisterType(typeof(MultiNodeWorkflow));

            List<Workflow> batch = new List<Workflow>();
            for (int i = 0; i < count; i++)
            {
                batch.Add(new MultiNodeWorkflow() { Priority = Random.Shared.Next(0, 3) });
                if (batch.Count == 200 || i == count - 1)
                {
                    workflowStore.Save(batch);
                    batch.Clear();
                }
            }

            new ExecutionLedger(store).RecordSeed(count);
            ConsoleWriter.WriteLine($"Seeded {count} workflows into the {store} store", ConsoleColor.Cyan);
        }

        private static async Task<int> RunNodeAsync(string store, Dictionary<string, string> options, CancellationToken cancellationToken = default)
        {
            string name = GetOption(options, "name", Environment.MachineName);
            int batchSize = int.Parse(GetOption(options, "batch", "200"));
            int concurrency = int.Parse(GetOption(options, "concurrency", "20"));
            bool runAsync = options.ContainsKey("async");
            int? crashAfter = options.ContainsKey("crash-after") ? int.Parse(options["crash-after"]) : null;
            TimeSpan nodeTimeout = TimeSpan.FromSeconds(int.Parse(GetOption(options, "node-timeout", "20")));
            TimeSpan maxRunTime = TimeSpan.FromMinutes(int.Parse(GetOption(options, "max-minutes", "30")));

            ExecutionLedger ledger = new ExecutionLedger(store);
            MultiNodeContext context = new MultiNodeContext(ledger, store, name, Path.Combine(Environment.CurrentDirectory, "logs", $"multinode-{store}-{name}.log"))
            {
                ErrorRate = double.Parse(GetOption(options, "error-rate", "0.02"), System.Globalization.CultureInfo.InvariantCulture),
                IoKb = int.Parse(GetOption(options, "io-kb", "256")),
                SlowStepRate = double.Parse(GetOption(options, "slow-rate", "0.005"), System.Globalization.CultureInfo.InvariantCulture),
                SlowStepDuration = nodeTimeout * 2
            };
            MultiNodeContext.Current = context;
            ledger.RegisterProcess(context.ProcessKey, name, crashAfter);

            WorkflowServerOptions serverOptions = new WorkflowServerOptions()
            {
                NodeName = name,
                HeartbeatInterval = nodeTimeout / 4,
                NodeTimeout = nodeTimeout,
                PersistWorkflowDefinitions = false
            };

            using (WorkflowServer server = new WorkflowServer(GetStore(store), serverOptions))
            {
                server.RegisterWorkflowType<MultiNodeWorkflow>();
                server.WorkflowClaimed += (sender, e) => context.Stats.RecordClaim();
                server.WorkflowCompleted += (sender, e) => context.Stats.RecordCompleted();
                server.WorkflowSuspended += (sender, e) => context.Log($"Workflow {e.Workflow.Id} SUSPENDED: {e.Workflow.LastException}", ConsoleColor.Red);
                server.WorkflowLockLost += (sender, e) =>
                {
                    context.Stats.RecordLockLost();
                    context.Log($"LOCK LOST: workflow {e.Workflow.Id} was claimed by another node while node {e.NodeId} was executing it", ConsoleColor.Red);
                };
                server.NodeEvicted += (sender, e) => context.Log($"Node was EVICTED: {e.PreviousNodeId} re-registered as {e.NodeId}", ConsoleColor.Yellow);
                server.NodeMonitorError += (sender, e) => context.Log($"Node monitor error: {e.Exception.Message}", ConsoleColor.Yellow);

                context.Log($"Node started on {store}: node id {server.NodeId}, batch {batchSize}, concurrency {concurrency}, {(runAsync ? "async" : "sync")}, node timeout {nodeTimeout.TotalSeconds}s, error rate {context.ErrorRate}" + (crashAfter.HasValue ? $", CRASHING after {crashAfter}s" : ""), ConsoleColor.Cyan);

                using Timer? crashTimer = crashAfter.HasValue ? new Timer(_ =>
                {
                    context.Log($"Planned crash - {context.Stats}", ConsoleColor.Magenta);
                    ledger.MarkCrashed(context.ProcessKey);
                    // a hard kill: no dispose, no lock release, and steps are interrupted mid-action
                    Process.GetCurrentProcess().Kill();
                }, null, crashAfter.Value * 1000, Timeout.Infinite) : null;
                using Timer statsTimer = new Timer(_ => context.Log(context.Stats.ToString()), null, 5000, 5000);

                Stopwatch stopwatch = Stopwatch.StartNew();
                int emptyChecks = 0;
                while (stopwatch.Elapsed < maxRunTime && !cancellationToken.IsCancellationRequested)
                {
                    int executed = 0;
                    try
                    {
                        executed = runAsync ? await server.ExecuteWorkflowsAsync(batchSize, concurrency, cancellationToken) : server.ExecuteWorkflows(batchSize, concurrency);
                    }
                    catch (Exception ex)
                    {
                        context.Log($"Error executing workflows: {ex.Message}", ConsoleColor.Red);
                    }

                    if (executed == 0)
                    {
                        // workflows locked by a crashed node only become available after the node timeout, so keep polling
                        // until the store is empty
                        emptyChecks = (server.WorkflowStore.GetIncompleteCount() == 0 ? emptyChecks + 1 : 0);
                        if (emptyChecks >= 3)
                        {
                            break;
                        }
                        cancellationToken.WaitHandle.WaitOne(1000);
                    }
                }

                if (stopwatch.Elapsed >= maxRunTime)
                {
                    context.Log($"Node stopped at its maximum run time of {maxRunTime.TotalMinutes:0} minutes - {server.WorkflowStore.GetIncompleteCount()} workflow(s) are still to run", ConsoleColor.Yellow);
                }
                context.Log($"Node finished in {stopwatch.Elapsed.TotalSeconds:0}s - {context.Stats}", ConsoleColor.Cyan);
            }
            return 0;
        }

        private static int Report(string store)
        {
            ExecutionLedger ledger = new ExecutionLedger(store);
            IWorkflowStore workflowStore = GetStore(store);
            workflowStore.RegisterType(typeof(MultiNodeWorkflow));
            int seeded = ledger.GetSeededCount();

            // RavenDB counts come from indexes, so give them a moment to catch up
            long incomplete = 0, suspended = 0, completed = 0;
            List<string> lockOwners = new List<string>();
            for (int attempt = 0; attempt < 5; attempt++)
            {
                incomplete = workflowStore.GetIncompleteCount();
                suspended = workflowStore.GetSuspendedCount();
                completed = workflowStore.GetCompletedCount();
                lockOwners = workflowStore.GetLockOwners().ToList();
                if (incomplete == 0 && completed == seeded)
                {
                    break;
                }
                Thread.Sleep(2000);
            }

            List<LedgerExecution> executions = ledger.GetExecutions();
            Dictionary<string, LedgerProcess> processes = ledger.GetProcesses().ToDictionary(x => x.ProcessKey);
            Func<LedgerExecution, DateTime?> crashedOn = e => processes.TryGetValue(e.ProcessKey, out LedgerProcess? p) ? p.CrashedOn : null;

            List<string> overlaps = new List<string>();
            List<string> duplicates = new List<string>();
            List<string> unfinished = new List<string>();
            int expectedReexecutions = 0;
            int interruptedByCrash = 0;

            foreach (LedgerExecution e in executions.Where(x => x.End == null))
            {
                if (crashedOn(e) != null)
                {
                    interruptedByCrash++;
                }
                else
                {
                    unfinished.Add($"{e.WorkflowId} {e.Step} on {e.NodeName} started {e.Start:HH:mm:ss.fff} never finished");
                }
            }

            foreach (var group in executions.GroupBy(x => (x.WorkflowId, x.Step)))
            {
                List<LedgerExecution> ordered = group.OrderBy(x => x.Start).ToList();
                for (int i = 0; i < ordered.Count; i++)
                {
                    DateTime endI = ordered[i].End ?? crashedOn(ordered[i]) ?? DateTime.MaxValue;
                    for (int j = i + 1; j < ordered.Count; j++)
                    {
                        if (endI > ordered[j].Start)
                        {
                            overlaps.Add($"{group.Key.WorkflowId} {group.Key.Step}: {ordered[i].NodeName} ({ordered[i].Start:HH:mm:ss.fff}-{endI:HH:mm:ss.fff}) and {ordered[j].NodeName} (from {ordered[j].Start:HH:mm:ss.fff})");
                        }
                    }
                }

                List<LedgerExecution> successes = ordered.Where(x => x.Outcome == "Success").ToList();
                for (int k = 1; k < successes.Count; k++)
                {
                    DateTime? previousCrash = crashedOn(successes[k - 1]);
                    if (previousCrash != null && successes[k].Start >= previousCrash)
                    {
                        // the node crashed after the step succeeded but before the workflow was saved - at-least-once
                        expectedReexecutions++;
                    }
                    else
                    {
                        duplicates.Add($"{group.Key.WorkflowId} {group.Key.Step}: succeeded on {successes[k - 1].NodeName} at {successes[k - 1].End:HH:mm:ss.fff} and again on {successes[k].NodeName} at {successes[k].End:HH:mm:ss.fff}");
                    }
                }
            }

            List<string> missingSteps = executions
                .GroupBy(x => x.WorkflowId)
                .Where(g => Steps.Any(step => !g.Any(x => x.Step == step && x.Outcome == "Success")))
                .Select(g => g.Key)
                .ToList();

            long liveOverlaps = ledger.GetLiveOverlapCount();
            string workFolder = MultiNodeContext.GetWorkFolder(store);
            int leftoverFolders = Directory.Exists(workFolder) ? Directory.GetDirectories(workFolder).Length : 0;

            bool pass = overlaps.Count == 0 && liveOverlaps == 0 && duplicates.Count == 0 && unfinished.Count == 0
                && incomplete == 0 && suspended == 0 && lockOwners.Count == 0 && completed == seeded && seeded > 0
                && missingSteps.Count == 0 && leftoverFolders == 0;

            ConsoleWriter.WriteLine($"=== Multi-node report: {store} ===", ConsoleColor.Cyan);
            Console.WriteLine($"Workflows: seeded {seeded}, completed {completed}, active {incomplete}, suspended {suspended}, locked by {lockOwners.Count} node(s)");
            Console.WriteLine();
            Console.WriteLine($"{"Node process",-45} {"Steps",8} {"Success",8} {"Errors",8} {"Crashed",8}");
            foreach (var node in executions.GroupBy(x => x.ProcessKey).OrderBy(x => x.Key))
            {
                string crashed = processes.TryGetValue(node.Key, out LedgerProcess? p) && p.CrashedOn != null ? "yes" : "";
                Console.WriteLine($"{node.Key,-45} {node.Count(),8} {node.Count(x => x.Outcome == "Success"),8} {node.Count(x => x.Outcome == "Exception"),8} {crashed,8}");
            }
            Console.WriteLine();
            Console.WriteLine($"Step executions: {executions.Count}, interrupted by a crash: {interruptedByCrash}, expected re-executions after a crash: {expectedReexecutions}");
            WriteViolations("Overlapping executions (same step on two nodes at once)", overlaps);
            WriteViolations("Overlaps detected live by the nodes", liveOverlaps == 0 ? new List<string>() : new List<string> { $"{liveOverlaps} overlap(s) - see the Overlaps collection in {ledger.DatabaseName}" });
            WriteViolations("Duplicate successful executions (not explained by a crash)", duplicates);
            WriteViolations("Executions that never finished (on nodes that did not crash)", unfinished);
            WriteViolations("Workflows missing a successful step", missingSteps);
            WriteViolations("Work folders left behind", leftoverFolders == 0 ? new List<string>() : new List<string> { $"{leftoverFolders} folder(s) in {workFolder}" });
            ConsoleWriter.WriteLine(pass ? "RESULT: PASS" : "RESULT: FAIL", pass ? ConsoleColor.Green : ConsoleColor.Red);

            var summary = new
            {
                Store = store,
                Result = pass ? "PASS" : "FAIL",
                Seeded = seeded,
                Completed = completed,
                Active = incomplete,
                Suspended = suspended,
                LockedByNodes = lockOwners.Count,
                StepExecutions = executions.Count,
                Overlaps = overlaps.Count + (int)liveOverlaps,
                DuplicateSuccesses = duplicates.Count,
                Unfinished = unfinished.Count,
                MissingSteps = missingSteps.Count,
                LeftoverFolders = leftoverFolders,
                InterruptedByCrash = interruptedByCrash,
                ExpectedReexecutions = expectedReexecutions
            };
            string reportPath = Path.Combine(Environment.CurrentDirectory, "logs", $"multinode-report-{store}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));

            return pass ? 0 : 1;
        }

        private static void WriteViolations(string title, List<string> violations)
        {
            if (violations.Count == 0)
            {
                ConsoleWriter.WriteLine($"  OK    {title}: none", ConsoleColor.Green);
                return;
            }
            ConsoleWriter.WriteLine($"  FAIL  {title}: {violations.Count}", ConsoleColor.Red);
            foreach (string violation in violations.Take(10))
            {
                Console.WriteLine($"          {violation}");
            }
            if (violations.Count > 10)
            {
                Console.WriteLine($"          ... and {violations.Count - 10} more");
            }
        }

        private static readonly Dictionary<string, IWorkflowStore> _stores = new Dictionary<string, IWorkflowStore>();

        /// <summary>
        /// Gets the store, creating it on first use - the interactive menu reads the store on every refresh, and a
        /// RavenDB document store should be created once and reused.
        /// </summary>
        private static IWorkflowStore GetStore(string store)
        {
            if (!_stores.TryGetValue(store, out IWorkflowStore? workflowStore))
            {
                workflowStore = CreateStore(store);
                workflowStore.RegisterType(typeof(MultiNodeWorkflow));
                _stores[store] = workflowStore;
            }
            return workflowStore;
        }

        private static IWorkflowStore CreateStore(string store)
        {
            if (store == StoreMongo)
            {
                return new MongoDbWorkflowStore(new MongoClient(AppSettings.MongoDbConnectionString).GetDatabase(DbName));
            }
            return new RavenDbWorkflowStore(CreateDocumentStore());
        }

        private static IDocumentStore CreateDocumentStore()
        {
            IDocumentStore documentStore = new DocumentStore
            {
                Urls = new[] { AppSettings.RavenDbUrl },
                Database = DbName
            };
            documentStore.Initialize();
            return documentStore;
        }

        private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
        {
            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> list = args.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].StartsWith("--"))
                {
                    continue;
                }
                string key = list[i].Substring(2);
                bool hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--");
                options[key] = hasValue ? list[++i] : "true";
            }
            return options;
        }

        private static string GetOption(Dictionary<string, string> options, string key, string defaultValue)
        {
            return options.TryGetValue(key, out string? value) ? value : defaultValue;
        }
    }
}
