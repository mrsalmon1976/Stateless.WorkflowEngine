using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Benchmark.Runner
{
    /// <summary>
    /// Finds the release zips and builds a benchmark agent for each of them, plus one for the current branch.  Each
    /// release is netstandard2.0 built against its own MongoDB driver, so every version gets its own agent executable
    /// compiled from the shared Benchmark.Agent source.
    /// </summary>
    public partial class AgentBuilder
    {
        private static readonly string[] DependencyIds = { "Stateless", "MongoDB.Driver", "Newtonsoft.Json" };

        private readonly RunSettings _settings;

        public AgentBuilder(RunSettings settings)
        {
            _settings = settings;
        }

        [GeneratedRegex(@"^Stateless\.WorkflowEngine\.MongoDb_v(?<version>[0-9.]+)\.zip$", RegexOptions.IgnoreCase)]
        private static partial Regex ReleaseZipRegex();

        public List<EngineVersion> DiscoverVersions()
        {
            List<EngineVersion> versions = new();
            string releasesFolder = Path.Combine(_settings.BenchmarksRoot, "releases");
            foreach (string zip in Directory.GetFiles(releasesFolder, "*.zip"))
            {
                Match match = ReleaseZipRegex().Match(Path.GetFileName(zip));
                if (!match.Success)
                {
                    Console.WriteLine($"Skipping {Path.GetFileName(zip)} - not a Stateless.WorkflowEngine.MongoDb_v<version>.zip release");
                    continue;
                }

                string name = match.Groups["version"].Value;
                string extractFolder = Path.Combine(_settings.WorkRoot, "releases", name);
                if (Directory.Exists(extractFolder))
                {
                    Directory.Delete(extractFolder, true);
                }
                ZipFile.ExtractToDirectory(zip, extractFolder);

                versions.Add(new EngineVersion
                {
                    Name = name,
                    Version = Version.Parse(name),
                    Dependencies = ReadDependencies(Path.Combine(extractFolder, "Stateless.WorkflowEngine.MongoDb.deps.json"))
                });
            }

            versions = versions.OrderBy(v => v.Version).ToList();
            versions.Add(new EngineVersion { Name = EngineVersion.CurrentName, Dependencies = ReadCurrentDependencies() });

            if (_settings.VersionFilter != null)
            {
                versions = versions.Where(v => _settings.VersionFilter.Contains(v.Name)).ToList();
            }
            return versions;
        }

        public void Build(EngineVersion version)
        {
            string projectFolder = Path.Combine(_settings.WorkRoot, "agents", version.Name);
            string outputFolder = Path.Combine(projectFolder, "bin");
            string projectPath = Path.Combine(projectFolder, "Benchmark.Agent.csproj");
            version.AgentDll = Path.Combine(outputFolder, "Benchmark.Agent.dll");

            if (_settings.SkipBuild && File.Exists(version.AgentDll))
            {
                return;
            }

            Directory.CreateDirectory(projectFolder);
            File.WriteAllText(projectPath, this.GenerateProject(version));

            Console.WriteLine($"Building agent for {version.Name}...");
            ProcessStartInfo psi = new("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in new[] { "build", projectPath, "-c", "Release", "-o", outputFolder, "-nologo", "-v", "q" })
            {
                psi.ArgumentList.Add(arg);
            }
            using Process process = Process.Start(psi)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Build of the {version.Name} agent failed:{Environment.NewLine}{stdout.Result}{stderr.Result}");
            }
        }

        private string GenerateProject(EngineVersion version)
        {
            string agentSource = Path.Combine(_settings.BenchmarksRoot, "src", "Benchmark.Agent");
            List<string> constants = new();
            if (version.IsLegacyExecute)
            {
                constants.Add("LEGACY_EXECUTE");
            }
            if (version.SupportsMultiNode)
            {
                constants.Add("MULTINODE");
            }

            string references;
            if (version.IsCurrent)
            {
                string project = Path.Combine(_settings.RepoRoot, "source", "Stateless.WorkflowEngine.MongoDb", "Stateless.WorkflowEngine.MongoDb.csproj");
                references = $"""
                    <ProjectReference Include="{project}" />
                """;
            }
            else
            {
                string releaseFolder = Path.Combine(_settings.WorkRoot, "releases", version.Name);
                IEnumerable<string> packages = version.Dependencies.Select(d => $"""    <PackageReference Include="{d.Key}" Version="{d.Value}" />""");
                references = $"""
                    <Reference Include="Stateless.WorkflowEngine"><HintPath>{Path.Combine(releaseFolder, "Stateless.WorkflowEngine.dll")}</HintPath></Reference>
                    <Reference Include="Stateless.WorkflowEngine.MongoDb"><HintPath>{Path.Combine(releaseFolder, "Stateless.WorkflowEngine.MongoDb.dll")}</HintPath></Reference>
                {string.Join(Environment.NewLine, packages)}
                """;
            }

            return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <!-- Generated by Benchmark.Runner for engine version {version.Name} - do not edit -->
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <AssemblyName>Benchmark.Agent</AssemblyName>
                    <RootNamespace>Benchmark.Agent</RootNamespace>
                    <Nullable>disable</Nullable>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    <DefineConstants>$(DefineConstants);{string.Join(";", constants)}</DefineConstants>
                    <NuGetAudit>false</NuGetAudit>
                    <TieredPGO>true</TieredPGO>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="{agentSource}\*.cs" />
                  </ItemGroup>
                  <ItemGroup>
                {references}
                  </ItemGroup>
                </Project>
                """;
        }

        private static Dictionary<string, string> ReadDependencies(string depsJsonPath)
        {
            Dictionary<string, string> dependencies = new();
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(depsJsonPath));
            foreach (JsonProperty library in doc.RootElement.GetProperty("libraries").EnumerateObject())
            {
                string[] parts = library.Name.Split('/');
                string? id = DependencyIds.FirstOrDefault(d => string.Equals(d, parts[0], StringComparison.OrdinalIgnoreCase));
                if (id != null && parts.Length == 2)
                {
                    dependencies[id] = parts[1];
                }
            }
            foreach (string id in DependencyIds.Where(d => !dependencies.ContainsKey(d)))
            {
                throw new InvalidOperationException($"{depsJsonPath} does not list a version of {id}");
            }
            return dependencies;
        }

        private Dictionary<string, string> ReadCurrentDependencies()
        {
            // informational only - the current agent uses project references
            Dictionary<string, string> dependencies = new();
            foreach (string project in new[] { "Stateless.WorkflowEngine", "Stateless.WorkflowEngine.MongoDb" })
            {
                string csproj = File.ReadAllText(Path.Combine(_settings.RepoRoot, "source", project, project + ".csproj"));
                foreach (Match match in Regex.Matches(csproj, @"<PackageReference\s+Include=""(?<id>[^""]+)""\s+Version=""(?<version>[^""]+)"""))
                {
                    string id = match.Groups["id"].Value;
                    if (DependencyIds.Contains(id))
                    {
                        dependencies[id] = match.Groups["version"].Value;
                    }
                }
            }
            return dependencies;
        }
    }
}
