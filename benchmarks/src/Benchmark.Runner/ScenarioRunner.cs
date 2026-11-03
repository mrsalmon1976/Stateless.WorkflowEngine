using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Benchmark.Runner
{
    /// <summary>
    /// Runs one iteration of a scenario: resets the database, starts the nodes, seeds the workflows, releases the nodes
    /// together and times them until every workflow has completed, then checks and measures the results.
    /// </summary>
    public class ScenarioRunner
    {
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

        private readonly RunSettings _settings;
        private readonly IMongoDatabase _database;

        public ScenarioRunner(RunSettings settings, MongoClient client)
        {
            _settings = settings;
            _database = client.GetDatabase(settings.Database);
        }

        public async Task<IterationResult> Run(EngineVersion version, Scenario scenario, int iteration)
        {
            IterationResult result = new() { Iteration = iteration };
            string tempFolder = Path.Combine(_settings.WorkRoot, "tmp");
            string logFolder = Path.Combine(_settings.WorkRoot, "logs", version.Name, $"scenario{scenario.Number}-run{iteration}");

            await _database.Client.DropDatabaseAsync(_settings.Database);
            this.ClearTempFiles(tempFolder);

            List<AgentProcess> nodes = new();
            try
            {
                for (int i = 1; i <= scenario.Nodes; i++)
                {
                    nodes.Add(this.StartNode(version, scenario, $"node{i}", tempFolder, logFolder));
                }

                await Task.WhenAll(nodes.Select(n => n.WaitFor("READY", StartupTimeout)));

                // seed once every node is up, so the time between registration and execution is just the seeding time
                nodes[0].Send($"SEED {_settings.Count}");
                result.SeedMs = double.Parse(await nodes[0].WaitFor("SEEDED", TimeSpan.FromMinutes(5)), CultureInfo.InvariantCulture);

                Stopwatch watch = Stopwatch.StartNew();
                foreach (AgentProcess node in nodes)
                {
                    node.Send("GO");
                }
                TimeSpan maxTime = TimeSpan.FromMinutes(_settings.MaxMinutes + 1);
                await Task.WhenAll(nodes.Select(n => n.WaitForExit(maxTime)));
                watch.Stop();
                result.TotalMs = watch.Elapsed.TotalMilliseconds;

                foreach (AgentProcess node in nodes)
                {
                    try
                    {
                        await node.WaitFor("RESULT", TimeSpan.FromSeconds(5));
                    }
                    catch (Exception)
                    {
                        // reported below
                    }
                    if (node.ResultJson != null)
                    {
                        result.Nodes.Add(JsonSerializer.Deserialize<NodeResult>(node.ResultJson)!);
                    }
                    else
                    {
                        result.Error = $"{node.Name} did not report a result - see {logFolder}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            finally
            {
                foreach (AgentProcess node in nodes)
                {
                    node.Dispose();
                }
            }

            await this.Measure(result);
            result.FilesLeft = this.CountTempFiles(tempFolder);
            return result;
        }

        private AgentProcess StartNode(EngineVersion version, Scenario scenario, string name, string tempFolder, string logFolder)
        {
            string logPath = Path.Combine(logFolder, name + ".log");
            List<string> agentArguments = new()
            {
                "--name", name,
                "--concurrency", scenario.Concurrency.ToString(),
                "--batch", _settings.Batch.ToString(),
                "--poll-ms", _settings.PollMs.ToString(),
                "--mode", _settings.Mode,
                "--max-minutes", _settings.MaxMinutes.ToString(),
                "--connection", _settings.AgentConnectionString,
                "--database", _settings.Database,
                "--work-folder", _settings.UseDocker ? Docker.WorkFolder : tempFolder
            };
            if (_settings.ClaimBatchSize.HasValue)
            {
                agentArguments.AddRange(new[] { "--claim-batch-size", _settings.ClaimBatchSize.Value.ToString() });
            }

            if (!_settings.UseDocker)
            {
                return new AgentProcess(name, "dotnet", new[] { version.AgentDll }.Concat(agentArguments), logPath);
            }

            // the agents folder is mounted read-only, and the temp files go on a docker volume shared by the nodes
            string agentsFolder = Path.Combine(_settings.WorkRoot, "agents");
            string agentDll = $"{Docker.AgentsFolder}/{version.Name}/bin/Benchmark.Agent.dll";
            string containerName = $"{Docker.ContainerPrefix}{version.Name.Replace('.', '-')}-{name}";
            List<string> dockerArguments = new()
            {
                "run", "-i", "--rm",
                "--name", containerName,
                "--network", _settings.DockerNetwork,
                "-v", $"{agentsFolder}:{Docker.AgentsFolder}:ro",
                "-v", $"{_settings.DockerVolume}:{Docker.WorkFolder}",
                _settings.DockerImage,
                "dotnet", agentDll
            };
            return new AgentProcess(name, "docker", dockerArguments.Concat(agentArguments), logPath, containerName);
        }

        private void ClearTempFiles(string tempFolder)
        {
            if (_settings.UseDocker)
            {
                Docker.RunInWorkVolume(_settings, $"rm -rf {Docker.WorkFolder}/*");
                return;
            }
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
            Directory.CreateDirectory(tempFolder);
        }

        private int CountTempFiles(string tempFolder)
        {
            if (_settings.UseDocker)
            {
                return int.Parse(Docker.RunInWorkVolume(_settings, $"ls -1 {Docker.WorkFolder} | wc -l"));
            }
            return Directory.GetFiles(tempFolder).Length;
        }

        private async Task Measure(IterationResult result)
        {
            IMongoCollection<BsonDocument> active = _database.GetCollection<BsonDocument>("Workflows");
            IMongoCollection<BsonDocument> completed = _database.GetCollection<BsonDocument>("CompletedWorkflows");

            result.Active = await active.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
            result.Suspended = await active.CountDocumentsAsync(new BsonDocument("Workflow.IsSuspended", true));

            ProjectionDefinition<BsonDocument> projection = new BsonDocument
            {
                { "Workflow.CreatedOn", 1 },
                { "Workflow.StartedOn", 1 },
                { "Workflow.CompletedOn", 1 }
            };
            List<BsonDocument> docs = await completed.Find(FilterDefinition<BsonDocument>.Empty).Project(projection).ToListAsync();
            result.Completed = docs.Count;

            List<double> execution = new();
            List<double> elapsed = new();
            DateTime firstStarted = DateTime.MaxValue;
            DateTime lastCompleted = DateTime.MinValue;
            foreach (BsonDocument doc in docs)
            {
                BsonDocument wf = doc["Workflow"].AsBsonDocument;
                if (!wf.TryGetValue("CompletedOn", out BsonValue completedOn) || completedOn.IsBsonNull)
                {
                    continue;
                }
                DateTime completedAt = completedOn.ToUniversalTime();
                lastCompleted = completedAt > lastCompleted ? completedAt : lastCompleted;
                elapsed.Add((completedAt - wf["CreatedOn"].ToUniversalTime()).TotalMilliseconds);
                if (wf.TryGetValue("StartedOn", out BsonValue startedOn) && !startedOn.IsBsonNull)
                {
                    DateTime startedAt = startedOn.ToUniversalTime();
                    firstStarted = startedAt < firstStarted ? startedAt : firstStarted;
                    execution.Add((completedAt - startedAt).TotalMilliseconds);
                }
            }
            result.Execution = Stats.From(execution);
            result.Elapsed = Stats.From(elapsed);
            result.RunElapsedMs = execution.Count > 0 ? (lastCompleted - firstStarted).TotalMilliseconds : 0;
        }
    }
}
