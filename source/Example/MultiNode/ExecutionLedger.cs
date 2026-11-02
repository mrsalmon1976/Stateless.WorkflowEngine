using MongoDB.Bson;
using MongoDB.Driver;

namespace Example.MultiNode
{
    /// <summary>
    /// Records every step execution of the multi-node stress test in MongoDB, independently of the workflow store being
    /// tested, so executions from all nodes (processes, machines or containers) can be compared.  The ledger lives in
    /// its own database per store type, so MongoDb and RavenDb runs never mix.
    /// </summary>
    public class ExecutionLedger
    {
        private readonly IMongoDatabase _database;

        public ExecutionLedger(string storeType)
        {
            MongoClient client = new MongoClient(AppSettings.MongoDbConnectionString);
            this.DatabaseName = $"StatelessMultiNodeLedger_{storeType}";
            _database = client.GetDatabase(this.DatabaseName);
        }

        public string DatabaseName { get; }

        /// <summary>
        /// One document per execution of a step: start, end and outcome.
        /// </summary>
        private IMongoCollection<BsonDocument> Executions => _database.GetCollection<BsonDocument>("Executions");

        /// <summary>
        /// One document per workflow step, listing the executions currently running - used to flag overlaps live.
        /// </summary>
        private IMongoCollection<BsonDocument> Running => _database.GetCollection<BsonDocument>("Running");

        /// <summary>
        /// One document per node process.
        /// </summary>
        private IMongoCollection<BsonDocument> Processes => _database.GetCollection<BsonDocument>("Processes");

        /// <summary>
        /// Overlaps detected while the test was running.
        /// </summary>
        private IMongoCollection<BsonDocument> Overlaps => _database.GetCollection<BsonDocument>("Overlaps");

        private IMongoCollection<BsonDocument> Runs => _database.GetCollection<BsonDocument>("Runs");

        public void Drop()
        {
            _database.Client.DropDatabase(this.DatabaseName);
        }

        public void RecordSeed(int count)
        {
            this.Runs.UpdateOne(new BsonDocument("_id", "run"), Builders<BsonDocument>.Update.Inc("Seeded", count).Set("SeededOn", DateTime.UtcNow), new UpdateOptions { IsUpsert = true });
        }

        public int GetSeededCount()
        {
            BsonDocument? run = this.Runs.Find(new BsonDocument("_id", "run")).FirstOrDefault();
            return run == null ? 0 : run["Seeded"].AsInt32;
        }

        public void RegisterProcess(string processKey, string nodeName, int? crashAfterSeconds)
        {
            this.Processes.InsertOne(new BsonDocument
            {
                { "_id", processKey },
                { "NodeName", nodeName },
                { "ProcessId", Environment.ProcessId },
                { "StartedOn", DateTime.UtcNow },
                { "PlannedCrash", crashAfterSeconds.HasValue },
                { "CrashedOn", BsonNull.Value }
            });
        }

        public void MarkCrashed(string processKey)
        {
            this.Processes.UpdateOne(new BsonDocument("_id", processKey), Builders<BsonDocument>.Update.Set("CrashedOn", DateTime.UtcNow));
        }

        /// <summary>
        /// Records the start of a step, and returns any other executions of the same step that are running right now on
        /// processes that haven't crashed.
        /// </summary>
        public (ObjectId ExecutionId, List<string> OverlappingNodes) BeginStep(Guid workflowId, string step, string processKey, string nodeName, string nodeId, int retryCount)
        {
            ObjectId executionId = ObjectId.GenerateNewId();
            DateTime start = DateTime.UtcNow;
            this.Executions.InsertOne(new BsonDocument
            {
                { "_id", executionId },
                { "WorkflowId", workflowId.ToString() },
                { "Step", step },
                { "ProcessKey", processKey },
                { "NodeName", nodeName },
                { "NodeId", nodeId },
                { "RetryCount", retryCount },
                { "Start", start },
                { "End", BsonNull.Value },
                { "Outcome", BsonNull.Value }
            });

            BsonDocument entry = new BsonDocument { { "ExecutionId", executionId }, { "ProcessKey", processKey }, { "NodeName", nodeName }, { "NodeId", nodeId }, { "Start", start } };
            BsonDocument running = this.Running.FindOneAndUpdate(
                new BsonDocument("_id", $"{workflowId}/{step}"),
                Builders<BsonDocument>.Update.Push("Executions", entry),
                new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

            List<BsonDocument> others = running["Executions"].AsBsonArray
                .Select(x => x.AsBsonDocument)
                .Where(x => x["ExecutionId"].AsObjectId != executionId)
                .ToList();
            if (others.Count > 0)
            {
                // executions left behind by a process that crashed mid-step are not overlaps
                HashSet<string> crashed = GetCrashedProcessKeys();
                others = others.Where(x => !crashed.Contains(x["ProcessKey"].AsString)).ToList();
            }
            if (others.Count > 0)
            {
                this.Overlaps.InsertOne(new BsonDocument
                {
                    { "WorkflowId", workflowId.ToString() },
                    { "Step", step },
                    { "DetectedOn", DateTime.UtcNow },
                    { "Execution", entry },
                    { "RunningExecutions", new BsonArray(others) }
                });
            }
            return (executionId, others.Select(x => $"{x["NodeName"]} ({x["NodeId"]})").ToList());
        }

        /// <summary>
        /// Records the end of a step.
        /// </summary>
        public void EndStep(ObjectId executionId, Guid workflowId, string step, bool success, string? error)
        {
            this.Executions.UpdateOne(new BsonDocument("_id", executionId), Builders<BsonDocument>.Update
                .Set("End", DateTime.UtcNow)
                .Set("Outcome", success ? "Success" : "Exception")
                .Set("Error", error == null ? BsonNull.Value : (BsonValue)error));

            this.Running.UpdateOne(new BsonDocument("_id", $"{workflowId}/{step}"),
                Builders<BsonDocument>.Update.PullFilter("Executions", Builders<BsonDocument>.Filter.Eq("ExecutionId", executionId)));
        }

        public List<LedgerExecution> GetExecutions()
        {
            return this.Executions.Find(new BsonDocument()).ToList().Select(x => new LedgerExecution
            {
                WorkflowId = x["WorkflowId"].AsString,
                Step = x["Step"].AsString,
                ProcessKey = x["ProcessKey"].AsString,
                NodeName = x["NodeName"].AsString,
                NodeId = x["NodeId"].AsString,
                Start = x["Start"].ToUniversalTime(),
                End = x["End"].IsBsonNull ? null : x["End"].ToUniversalTime(),
                Outcome = x["Outcome"].IsBsonNull ? null : x["Outcome"].AsString
            }).ToList();
        }

        public List<LedgerProcess> GetProcesses()
        {
            return this.Processes.Find(new BsonDocument()).ToList().Select(x => new LedgerProcess
            {
                ProcessKey = x["_id"].AsString,
                NodeName = x["NodeName"].AsString,
                PlannedCrash = x["PlannedCrash"].AsBoolean,
                CrashedOn = x["CrashedOn"].IsBsonNull ? null : x["CrashedOn"].ToUniversalTime()
            }).ToList();
        }

        public long GetLiveOverlapCount()
        {
            return this.Overlaps.CountDocuments(new BsonDocument());
        }

        private HashSet<string> GetCrashedProcessKeys()
        {
            return this.Processes.Find(Builders<BsonDocument>.Filter.Ne("CrashedOn", BsonNull.Value))
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .ToList()
                .Select(x => x["_id"].AsString)
                .ToHashSet();
        }
    }

    public class LedgerExecution
    {
        public string WorkflowId { get; set; } = "";
        public string Step { get; set; } = "";
        public string ProcessKey { get; set; } = "";
        public string NodeName { get; set; } = "";
        public string NodeId { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string? Outcome { get; set; }
    }

    public class LedgerProcess
    {
        public string ProcessKey { get; set; } = "";
        public string NodeName { get; set; } = "";
        public bool PlannedCrash { get; set; }
        public DateTime? CrashedOn { get; set; }
    }
}
