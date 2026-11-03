using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Benchmark.Runner
{
    /// <summary>
    /// Writes the markdown benchmark report and the raw JSON results.
    /// </summary>
    public class ReportWriter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly RunSettings _settings;
        private readonly EnvironmentInfo _environment;
        private readonly List<EngineVersion> _versions;
        private readonly List<ScenarioResult> _results;
        private readonly DateTime _startedAt;
        private readonly TimeSpan _duration;

        public ReportWriter(RunSettings settings, EnvironmentInfo environment, List<EngineVersion> versions, List<ScenarioResult> results, DateTime startedAt, TimeSpan duration)
        {
            _settings = settings;
            _environment = environment;
            _versions = versions;
            _results = results;
            _startedAt = startedAt;
            _duration = duration;
        }

        public string Write()
        {
            string folder = _settings.ReportsFolder ?? Path.Combine(_settings.BenchmarksRoot, "reports");
            Directory.CreateDirectory(folder);
            string baseName = $"benchmark-{_startedAt:yyyyMMdd-HHmm}";
            string reportPath = Path.Combine(folder, baseName + ".md");

            File.WriteAllText(reportPath, this.BuildMarkdown());
            File.WriteAllText(Path.Combine(folder, baseName + ".json"), JsonSerializer.Serialize(new
            {
                StartedAt = _startedAt,
                Settings = new { _settings.Count, _settings.Iterations, _settings.Batch, _settings.PollMs, _settings.Mode, _settings.Database },
                Environment = _environment,
                Versions = _versions.Select(v => new { v.Name, ExecutionApi = v.ExecutionApi(_settings.Mode), v.SupportsMultiNode, v.Dependencies }),
                Results = _results
            }, new JsonSerializerOptions { WriteIndented = true }));
            return reportPath;
        }

        private string BuildMarkdown()
        {
            StringBuilder md = new();
            EngineVersion? baseline = _versions.Where(v => !v.IsCurrent).OrderBy(v => v.Version).LastOrDefault();
            List<Scenario> scenarios = _results.Select(r => r.Scenario).Distinct().OrderBy(s => s.Number).ToList();

            md.AppendLine($"# Benchmark report - {_startedAt:yyyy-MM-dd HH:mm}");
            md.AppendLine();
            md.AppendLine($"Each scenario runs {_settings.Count} two-step workflows (write a {WriteFileSizeKb} KB file of random data, then delete it) through MongoDB, "
                + $"{_settings.Iterations} times per version. Reported values are the median of the iterations.");
            md.AppendLine();

            md.AppendLine("## Run details");
            md.AppendLine();
            md.AppendLine("| | |");
            md.AppendLine("|---|---|");
            md.AppendLine($"| Started | {_startedAt:yyyy-MM-dd HH:mm:ss} (run took {FormatDuration(_duration.TotalMilliseconds)}) |");
            md.AppendLine($"| Branch | `{_environment.GitBranch}` @ `{_environment.GitCommit}`{(_environment.GitDirty ? " (uncommitted changes)" : "")} |");
            md.AppendLine($"| CPU | {_environment.Cpu} ({_environment.LogicalCores} logical cores) |");
            md.AppendLine($"| OS | {_environment.Os} |");
            md.AppendLine($"| Runtime | {_environment.Runtime} |");
            md.AppendLine($"| MongoDB | {_environment.MongoVersion} on {_environment.MongoServer} |");
            md.AppendLine($"| Nodes run in | {_environment.NodeHost} |");
            md.AppendLine($"| Workflows per run | {_settings.Count} |");
            md.AppendLine($"| Iterations | {_settings.Iterations} |");
            md.AppendLine($"| Execution mode | {_settings.Mode} |");
            md.AppendLine($"| Claim batch size | {(_settings.ClaimBatchSize.HasValue ? _settings.ClaimBatchSize.Value.ToString(Invariant) : "version default")} |");
            md.AppendLine($"| Batch size | {_settings.Batch} (`count` passed to `ExecuteWorkflows[Async]`) |");
            md.AppendLine($"| Idle poll interval | {_settings.PollMs} ms |");
            md.AppendLine();

            md.AppendLine("## Versions");
            md.AppendLine();
            md.AppendLine("| Version | Stateless | MongoDB.Driver | Newtonsoft.Json | Execution API | Multi-node |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (EngineVersion v in _versions)
            {
                md.AppendLine($"| {v.Name} | {Dep(v, "Stateless")} | {Dep(v, "MongoDB.Driver")} | {Dep(v, "Newtonsoft.Json")} | `{v.ExecutionApi(_settings.Mode)}` | {(v.SupportsMultiNode ? "yes" : "no")} |");
            }
            md.AppendLine();

            this.AppendSummary(md, scenarios, $"Summary - time to complete {_settings.Count} workflows",
                "*Run elapsed*: from the first workflow starting to the last workflow completing (from the stored `StartedOn`/`CompletedOn`).",
                i => i.RunElapsedMs);
            this.AppendSummary(md, scenarios, "Summary - total run time",
                "*Total time*: from releasing the nodes until the last node exits - adds node start-up of the run and the final idle poll.",
                i => i.TotalMs);

            this.AppendRegressionCheck(md, baseline, scenarios);
            this.AppendSameConcurrency(md, scenarios);

            md.AppendLine("## Scenarios");
            md.AppendLine();
            md.AppendLine("*Run elapsed* is the time to complete all the workflows (first start to last completion). *Execution* is `CompletedOn - StartedOn` "
                + "(start of the first step to completion). *Elapsed* is `CompletedOn - CreatedOn`, which adds the time the workflow waited in the queue - all "
                + "workflows are registered before the run starts, so it mostly shows how quickly the queue drains. *vs current* is how much longer (+) or "
                + "shorter (-) the version's run elapsed is than the current branch.");
            md.AppendLine();
            foreach (Scenario s in scenarios)
            {
                md.AppendLine($"### {s.Number}. {_settings.Count} workflows - {s.Title}");
                md.AppendLine();
                md.AppendLine("| Version | Run elapsed | Total time | Throughput | Avg execution | p50 execution | p95 execution | Avg elapsed | p95 elapsed | vs current |");
                md.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
                ScenarioResult? current = _results.FirstOrDefault(r => r.Version == EngineVersion.CurrentName && r.Scenario == s);
                foreach (EngineVersion v in _versions)
                {
                    ScenarioResult? r = this.Find(v, s);
                    if (r == null)
                    {
                        md.AppendLine($"| {Label(v)} | n/a | | | | | | | | |");
                        continue;
                    }
                    double runElapsed = r.Median(i => i.RunElapsedMs);
                    string vsCurrent = v.IsCurrent || current == null ? "" : FormatPercent(Change(current.Median(i => i.RunElapsedMs), runElapsed));
                    md.AppendLine($"| {Label(v)} | {FormatDuration(runElapsed)}{this.FailMark(r)} | {FormatDuration(r.Median(i => i.TotalMs))} "
                        + $"| {r.Median(i => i.Throughput).ToString("0.0", Invariant)} wf/s "
                        + $"| {FormatMs(r.Median(i => i.Execution.Mean))} | {FormatMs(r.Median(i => i.Execution.P50))} | {FormatMs(r.Median(i => i.Execution.P95))} "
                        + $"| {FormatDuration(r.Median(i => i.Elapsed.Mean))} | {FormatDuration(r.Median(i => i.Elapsed.P95))} | {vsCurrent} |");
                }
                md.AppendLine();
            }

            md.AppendLine("## Iterations");
            md.AppendLine();
            md.AppendLine("| Version | Scenario | Run | Run elapsed | Total time | Seed time | Avg execution | Avg elapsed | Completed | Workflows per node | Checks |");
            md.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|");
            foreach (ScenarioResult r in _results)
            {
                foreach (IterationResult i in r.Iterations)
                {
                    string perNode = string.Join(", ", i.Nodes.OrderBy(n => n.Name).Select(n => n.Executed.ToString(Invariant)));
                    md.AppendLine($"| {r.Version} | {r.Scenario.Number}. {r.Scenario.ShortTitle} | {i.Iteration} | {FormatDuration(i.RunElapsedMs)} | {FormatDuration(i.TotalMs)} "
                        + $"| {FormatDuration(i.SeedMs)} | {FormatMs(i.Execution.Mean)} | {FormatDuration(i.Elapsed.Mean)} | {i.Completed} | {perNode} | {this.Checks(i)} |");
                }
            }
            md.AppendLine();

            md.AppendLine("## Notes");
            md.AppendLine();
            md.AppendLine(_settings.UseDocker
                ? "- Nodes run in containers on the MongoDB container's network: Docker Desktop on Windows adds a 40 ms stall to "
                    + "host-to-container requests over ~750 bytes, which would penalise whichever version's saves happen to cross that size."
                : "- Nodes ran as host processes (`--host local`). If MongoDB is in Docker Desktop, requests over ~750 bytes stall for 40 ms, "
                    + "which distorts the comparison - prefer the default `--host docker`.");
            md.AppendLine("- Every node is a separate process, started and connected before the workflows are registered. The total time runs from releasing the nodes "
                + "until the last node exits, which it does once no unsuspended workflows remain.");
            md.AppendLine($"- Versions from 5.0 use `{(_settings.Mode == "sync" ? "ExecuteWorkflows" : "ExecuteWorkflowsAsync")}(batch, concurrency)` in a loop (`--mode`)."
                + " Versions before 5.0 have no async API or `maxConcurrent`, "
                + "so they call `ExecuteWorkflows(concurrency)`, which loads that many workflows and runs them in parallel, waiting for the whole batch before loading the next.");
            md.AppendLine("- Releases before 6.0 have no workflow locking, so two nodes would execute the same workflows - multi-node scenarios are n/a for them.");
            md.AppendLine("- Checks: every workflow completed, none active or suspended, no temp files left behind, and no node timed out. A failed check is marked ⚠.");
            md.AppendLine("- The actions are synchronous (`IWorkflowAction`) for every version, so differences come from the engine and store, not the action code.");
            return md.ToString();
        }

        private void AppendSummary(StringBuilder md, List<Scenario> scenarios, string title, string description, Func<IterationResult, double> selector)
        {
            md.AppendLine($"## {title}");
            md.AppendLine();
            md.AppendLine(description);
            md.AppendLine();
            md.AppendLine("| Version | " + string.Join(" | ", scenarios.Select(s => $"{s.Number}. {s.Title}")) + " |");
            md.AppendLine("|---|" + string.Concat(scenarios.Select(_ => "---:|")));
            foreach (EngineVersion v in _versions)
            {
                IEnumerable<string> cells = scenarios.Select(s =>
                {
                    ScenarioResult? r = this.Find(v, s);
                    return r == null ? "n/a" : FormatDuration(r.Median(selector)) + this.FailMark(r);
                });
                md.AppendLine($"| {Label(v)} | {string.Join(" | ", cells)} |");
            }
            md.AppendLine();
        }

        /// <summary>
        /// Compares one node with several nodes running the same total number of workflows at once (e.g. 1×4 vs 2×2), for
        /// the versions that support multiple nodes.
        /// </summary>
        private void AppendSameConcurrency(StringBuilder md, List<Scenario> scenarios)
        {
            List<(Scenario Single, Scenario Multi)> pairs = scenarios
                .Where(m => m.Nodes > 1)
                .SelectMany(m => scenarios.Where(s => s.Nodes == 1 && s.TotalConcurrency == m.TotalConcurrency).Select(s => (s, m)))
                .ToList();
            List<EngineVersion> versions = _versions.Where(v => v.SupportsMultiNode).ToList();
            if (pairs.Count == 0 || versions.Count == 0)
            {
                return;
            }

            md.AppendLine("## One node vs multiple nodes at the same total concurrency");
            md.AppendLine();
            md.AppendLine("The same number of workflows run at once, either on one node or split across nodes. All nodes share this machine's CPU and "
                + "the one MongoDB server, so this shows the cost of coordinating nodes rather than the gain from adding machines.");
            md.AppendLine();
            md.AppendLine("| Version | Total concurrency | One node | Run elapsed | Avg execution | Multiple nodes | Run elapsed | Avg execution | Change |");
            md.AppendLine("|---|---:|---|---:|---:|---|---:|---:|---:|");
            foreach (EngineVersion v in versions)
            {
                foreach ((Scenario single, Scenario multi) in pairs)
                {
                    ScenarioResult? s = this.Find(v, single);
                    ScenarioResult? m = this.Find(v, multi);
                    if (s == null || m == null)
                    {
                        continue;
                    }
                    double singleElapsed = s.Median(i => i.RunElapsedMs);
                    double multiElapsed = m.Median(i => i.RunElapsedMs);
                    md.AppendLine($"| {Label(v)} | {single.TotalConcurrency} | {single.ShortTitle} | {FormatDuration(singleElapsed)}{this.FailMark(s)} "
                        + $"| {FormatMs(s.Median(i => i.Execution.Mean))} | {multi.ShortTitle} | {FormatDuration(multiElapsed)}{this.FailMark(m)} "
                        + $"| {FormatMs(m.Median(i => i.Execution.Mean))} | {FormatPercent(Change(singleElapsed, multiElapsed))} |");
                }
            }
            md.AppendLine();
        }

        private void AppendRegressionCheck(StringBuilder md, EngineVersion? baseline, List<Scenario> scenarios)
        {
            if (baseline == null || !_versions.Any(v => v.IsCurrent))
            {
                return;
            }

            md.AppendLine($"## Regression check - current vs {baseline.Name}");
            md.AppendLine();
            md.AppendLine($"Flags a scenario when the current branch is more than {_settings.RegressionThreshold.ToString("0.#", Invariant)}% slower than the latest release.");
            md.AppendLine();
            md.AppendLine($"| Scenario | Run elapsed ({baseline.Name}) | Run elapsed (current) | Change | Avg execution ({baseline.Name}) | Avg execution (current) | Change | Result |");
            md.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");
            foreach (Scenario s in scenarios)
            {
                ScenarioResult? release = this.Find(baseline, s);
                ScenarioResult? current = _results.FirstOrDefault(r => r.Version == EngineVersion.CurrentName && r.Scenario == s);
                if (current == null)
                {
                    continue;
                }
                if (release == null)
                {
                    md.AppendLine($"| {s.Number}. {s.Title} | n/a | {FormatDuration(current.Median(i => i.RunElapsedMs))} | | n/a | {FormatMs(current.Median(i => i.Execution.Mean))} | | no baseline |");
                    continue;
                }

                double releaseElapsed = release.Median(i => i.RunElapsedMs);
                double currentElapsed = current.Median(i => i.RunElapsedMs);
                double releaseExec = release.Median(i => i.Execution.Mean);
                double currentExec = current.Median(i => i.Execution.Mean);
                double elapsedChange = Change(releaseElapsed, currentElapsed);
                double execChange = Change(releaseExec, currentExec);
                bool regressed = elapsedChange > _settings.RegressionThreshold || execChange > _settings.RegressionThreshold;
                string outcome = !current.AllPassed(_settings.Count) ? "⚠ checks failed" : regressed ? "⚠ slower" : "✅ ok";
                md.AppendLine($"| {s.Number}. {s.Title} | {FormatDuration(releaseElapsed)} | {FormatDuration(currentElapsed)} | {FormatPercent(elapsedChange)} "
                    + $"| {FormatMs(releaseExec)} | {FormatMs(currentExec)} | {FormatPercent(execChange)} | {outcome} |");
            }
            md.AppendLine();
        }

        private const int WriteFileSizeKb = 100;

        private ScenarioResult? Find(EngineVersion version, Scenario scenario)
        {
            return _results.FirstOrDefault(r => r.Version == version.Name && r.Scenario == scenario);
        }

        private string FailMark(ScenarioResult result)
        {
            return result.AllPassed(_settings.Count) ? "" : " ⚠";
        }

        private string Checks(IterationResult i)
        {
            if (i.Passed(_settings.Count))
            {
                return "✅";
            }
            List<string> problems = new();
            if (i.Error != null)
            {
                problems.Add(i.Error.Replace("|", "/").ReplaceLineEndings(" "));
            }
            if (i.Completed != _settings.Count)
            {
                problems.Add($"{i.Completed} completed");
            }
            if (i.Active > 0)
            {
                problems.Add($"{i.Active} active");
            }
            if (i.Suspended > 0)
            {
                problems.Add($"{i.Suspended} suspended");
            }
            if (i.FilesLeft > 0)
            {
                problems.Add($"{i.FilesLeft} files left");
            }
            if (i.Nodes.Any(n => n.TimedOut))
            {
                problems.Add("node timed out");
            }
            return "⚠ " + string.Join("; ", problems);
        }

        private static string Label(EngineVersion v) => v.IsCurrent ? "**current**" : v.Name;

        private static string Dep(EngineVersion v, string id) => v.Dependencies.TryGetValue(id, out string? version) ? version : "-";

        /// <summary>
        /// Percentage change of <c>value</c> relative to <c>reference</c>.
        /// </summary>
        private static double Change(double reference, double value) => reference == 0 ? 0 : (value - reference) / reference * 100;

        private static string FormatPercent(double change) => (change >= 0 ? "+" : "") + change.ToString("0.0", Invariant) + "%";

        private static string FormatMs(double ms) => ms.ToString("0.0", Invariant) + " ms";

        private static string FormatDuration(double ms)
        {
            if (ms < 1000)
            {
                return ms.ToString("0", Invariant) + " ms";
            }
            if (ms < 120_000)
            {
                return (ms / 1000).ToString("0.00", Invariant) + " s";
            }
            TimeSpan ts = TimeSpan.FromMilliseconds(ms);
            return $"{(int)ts.TotalMinutes}m {ts.Seconds:00}s";
        }
    }
}
