using Example.Shared;

namespace Example.MultiNode
{
    /// <summary>
    /// Settings and services for the multi-node workflow actions running in this process.  Workflow actions are created
    /// by the engine, so they read these from <c>Current</c>.
    /// </summary>
    public class MultiNodeContext
    {
        private readonly object _logLock = new object();
        private readonly string? _logFilePath;

        public MultiNodeContext(ExecutionLedger ledger, string storeType, string nodeName, string? logFilePath)
        {
            this.Ledger = ledger;
            this.StoreType = storeType;
            this.NodeName = nodeName;
            this.ProcessKey = $"{nodeName}-{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
            this.WorkFolder = GetWorkFolder(storeType);
            _logFilePath = logFilePath;
            if (_logFilePath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
            }
        }

        /// <summary>
        /// Gets/sets the context for this process.
        /// </summary>
        public static MultiNodeContext Current { get; set; } = null!;

        public ExecutionLedger Ledger { get; }

        public string StoreType { get; }

        public string NodeName { get; }

        /// <summary>
        /// Gets the unique key of this process in the ledger - a node name can be reused when a node is restarted.
        /// </summary>
        public string ProcessKey { get; }

        public string WorkFolder { get; }

        public double ErrorRate { get; set; } = 0.02;

        public int IoKb { get; set; } = 256;

        public int MinDelayMs { get; set; } = 50;

        public int MaxDelayMs { get; set; } = 300;

        public double SlowStepRate { get; set; } = 0.005;

        public TimeSpan SlowStepDuration { get; set; } = TimeSpan.FromSeconds(40);

        public MultiNodeStats Stats { get; } = new MultiNodeStats();

        public static string GetWorkFolder(string storeType)
        {
            return Path.Combine(Path.GetTempPath(), "StatelessMultiNode", storeType);
        }

        public void Log(string message, ConsoleColor? color = null)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [{this.NodeName}] {message}";
            ConsoleWriter.WriteLine(line, color);
            if (_logFilePath != null)
            {
                lock (_logLock)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
        }
    }

    /// <summary>
    /// Counters for the steps executed by this process.
    /// </summary>
    public class MultiNodeStats
    {
        private long _stepsSucceeded;
        private long _stepsFailed;
        private long _workflowsClaimed;
        private long _workflowsCompleted;
        private long _locksLost;

        public long StepsSucceeded => Interlocked.Read(ref _stepsSucceeded);

        public long StepsFailed => Interlocked.Read(ref _stepsFailed);

        public long WorkflowsClaimed => Interlocked.Read(ref _workflowsClaimed);

        public long WorkflowsCompleted => Interlocked.Read(ref _workflowsCompleted);

        public long LocksLost => Interlocked.Read(ref _locksLost);

        public void RecordStep(bool success)
        {
            Interlocked.Increment(ref success ? ref _stepsSucceeded : ref _stepsFailed);
        }

        public void RecordClaim()
        {
            Interlocked.Increment(ref _workflowsClaimed);
        }

        public void RecordCompleted()
        {
            Interlocked.Increment(ref _workflowsCompleted);
        }

        public void RecordLockLost()
        {
            Interlocked.Increment(ref _locksLost);
        }

        public override string ToString()
        {
            return $"claimed {this.WorkflowsClaimed}, completed {this.WorkflowsCompleted}, steps ok {this.StepsSucceeded}, steps failed {this.StepsFailed}, locks lost {this.LocksLost}";
        }
    }
}
