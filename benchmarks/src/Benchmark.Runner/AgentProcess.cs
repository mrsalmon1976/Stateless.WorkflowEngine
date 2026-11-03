using System.Diagnostics;
using System.Threading.Channels;

namespace Benchmark.Runner
{
    /// <summary>
    /// A running benchmark agent (node).  Its stdout is read line by line for the protocol messages, and all output is
    /// written to a log file.
    /// </summary>
    public sealed class AgentProcess : IDisposable
    {
        private readonly Process _process;
        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
        private readonly StreamWriter _log;
        private bool _disposed;

        private readonly string? _containerName;

        /// <param name="fileName">dotnet for a local node, or docker for a containerised one.</param>
        /// <param name="containerName">The container the node runs in, so it can be removed if the node is killed.</param>
        public AgentProcess(string name, string fileName, IEnumerable<string> arguments, string logPath, string? containerName = null)
        {
            this.Name = name;
            _containerName = containerName;
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            _log = new StreamWriter(logPath) { AutoFlush = true };

            ProcessStartInfo psi = new(fileName)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            _process = new Process { StartInfo = psi };
            _process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    _lines.Writer.TryComplete();
                    return;
                }
                this.WriteLog(e.Data);
                _lines.Writer.TryWrite(e.Data);
            };
            _process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    this.WriteLog("ERR " + e.Data);
                }
            };
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public string Name { get; }

        public string? ResultJson { get; private set; }

        public void Send(string line)
        {
            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }

        /// <summary>
        /// Waits for a stdout line starting with <c>prefix</c> and returns the rest of the line.
        /// </summary>
        public async Task<string> WaitFor(string prefix, TimeSpan timeout)
        {
            using CancellationTokenSource cts = new(timeout);
            try
            {
                while (await _lines.Reader.WaitToReadAsync(cts.Token))
                {
                    while (_lines.Reader.TryRead(out string? line))
                    {
                        if (line.StartsWith("RESULT "))
                        {
                            this.ResultJson = line.Substring(7);
                        }
                        if (line.StartsWith(prefix))
                        {
                            return line.Substring(prefix.Length).Trim();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Node {this.Name} did not send {prefix} within {timeout.TotalSeconds}s");
            }
            throw new InvalidOperationException($"Node {this.Name} exited (code {this.ExitCodeOrNull()}) before sending {prefix} - see its log");
        }

        public async Task<int> WaitForExit(TimeSpan timeout)
        {
            using CancellationTokenSource cts = new(timeout);
            try
            {
                await _process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                this.Kill();
                throw new TimeoutException($"Node {this.Name} did not exit within {timeout.TotalMinutes} minutes and was killed");
            }
            return _process.ExitCode;
        }

        public void Kill()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                    if (_containerName != null)
                    {
                        // killing the docker client doesn't stop the container
                        Docker.Run("rm", "-f", _containerName);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // already gone
            }
        }

        public void Dispose()
        {
            this.Kill();
            _process.Dispose();
            lock (_log)
            {
                _disposed = true;
                _log.Dispose();
            }
        }

        private int? ExitCodeOrNull()
        {
            return _process.HasExited ? _process.ExitCode : null;
        }

        private void WriteLog(string line)
        {
            lock (_log)
            {
                if (!_disposed)
                {
                    _log.WriteLine(line);
                }
            }
        }
    }
}
