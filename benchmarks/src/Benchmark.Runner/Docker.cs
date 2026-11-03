using System.Diagnostics;

namespace Benchmark.Runner
{
    /// <summary>
    /// Thin wrapper over the docker CLI, used when the nodes run in containers.
    /// </summary>
    public static class Docker
    {
        public const string ContainerPrefix = "stateless-benchmark-";
        public const string WorkFolder = "/work";
        public const string AgentsFolder = "/agents";

        public static string Run(params string[] arguments)
        {
            ProcessStartInfo psi = new("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }
            using Process process = Process.Start(psi)!;
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"docker {string.Join(" ", arguments)} failed: {stderr.Result.Trim()}");
            }
            return output.Trim();
        }

        /// <summary>
        /// Pulls the image, finds the MongoDB container's network and removes containers left by an aborted run.
        /// </summary>
        public static void Prepare(RunSettings settings)
        {
            Console.WriteLine($"Preparing docker ({settings.DockerImage})...");
            Run("pull", "-q", settings.DockerImage);
            string networks = Run("inspect", settings.DockerMongoContainer, "--format", "{{range $k, $v := .NetworkSettings.Networks}}{{$k}} {{end}}");
            settings.DockerNetwork = networks.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                ?? throw new InvalidOperationException($"Container {settings.DockerMongoContainer} is not on a docker network");
            RemoveContainers(settings);
        }

        /// <summary>
        /// Removes the benchmark's containers - including any left behind by an aborted run, which would keep the work
        /// volume in use.
        /// </summary>
        public static void RemoveContainers(RunSettings settings)
        {
            IEnumerable<string> ids = Lines(Run("ps", "-aq", "--filter", "name=" + ContainerPrefix))
                .Concat(Lines(Run("ps", "-aq", "--filter", "volume=" + settings.DockerVolume)))
                .Distinct()
                .ToList();
            if (ids.Any())
            {
                Run(new[] { "rm", "-f" }.Concat(ids).ToArray());
            }
        }

        /// <summary>
        /// Removes the benchmark's containers and the work volume.  Failures are reported as warnings, as the results have
        /// already been written.
        /// </summary>
        public static void CleanUp(RunSettings settings)
        {
            try
            {
                RemoveContainers(settings);
                Run("volume", "rm", "-f", settings.DockerVolume);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Warning: docker clean up failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Runs a shell command in a throwaway container with the work volume mounted.  The container is named with the
        /// benchmark prefix, so it is removed by <see cref="RemoveContainers"/> if the run is aborted.
        /// </summary>
        public static string RunInWorkVolume(RunSettings settings, string command)
        {
            string name = $"{ContainerPrefix}work-{Guid.NewGuid():N}";
            return Run("run", "--rm", "--name", name, "-v", $"{settings.DockerVolume}:{WorkFolder}", settings.DockerImage, "sh", "-c", command);
        }

        private static IEnumerable<string> Lines(string output)
        {
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        public static string Info()
        {
            return Run("info", "--format", "{{.OperatingSystem}}, {{.NCPU}} CPUs, {{.MemTotal}} bytes memory");
        }
    }
}
