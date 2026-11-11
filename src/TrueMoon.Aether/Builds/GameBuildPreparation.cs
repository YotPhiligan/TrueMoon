using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml;

namespace TrueMoon.Aether.Builds;

// One compiler job owns the copies and read leases. Disposal releases leases; compiler cleanup removes staging.
internal sealed class GameBuildPreparation : IDisposable
{
    private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> Properties = new(StringComparer.OrdinalIgnoreCase)
    {
        "TargetFramework", "TF", "TestsTF", "OutputType", "AssemblyName", "RootNamespace", "ImplicitUsings", "Nullable",
        "LangVersion", "AllowUnsafeBlocks", "DefineConstants", "Optimize", "CheckForOverflowUnderflow", "WarningLevel",
        "NoWarn", "WarningsAsErrors", "WarningsNotAsErrors", "TreatWarningsAsErrors", "DebugType", "DebugSymbols",
        "Deterministic", "PlatformTarget", "AnalysisLevel", "AnalysisMode", "RunAnalyzersDuringBuild", "EnableNETAnalyzers",
        "GenerateDocumentationFile", "GenerateAssemblyInfo", "EnableDefaultItems", "EnableDefaultCompileItems",
        "EnableDefaultEmbeddedResourceItems", "EnableDefaultNoneItems", "DefaultItemExcludes", "IsPackable", "Version",
        "VersionPrefix", "VersionSuffix", "Authors", "Company", "Product", "Description", "Copyright", "PackageTags",
        "PackageProjectUrl", "RepositoryUrl", "RepositoryType", "PackageLicenseFile", "PackageReadmeFile", "PackageIcon",
        "TieredCompilationQuickJit", "TieredPGO", "RestorePackagesWithLockFile", "RestoreLockedMode", "Features"
    };
    private readonly string _job, _directory, _projectDirectory, _excluded;
    private readonly Dictionary<string, string> _roots = new(Paths);
    private readonly Dictionary<string, GameBuildInputFile> _files = new(Paths);
    private readonly Dictionary<string, GameBuildSourceWatchProbe> _probes = new(Paths);
    private readonly HashSet<string> _treeFiles = new(Paths);
    private readonly GameBuildSourceWatchPolicy _sourcePolicy;
    private string? _sourceWatchUnavailableReason;
    private readonly HashSet<string> _projects = new(Paths), _visiting = new(Paths), _copiedDirectories = new(Paths);
    private readonly Dictionary<string, Project> _graph = new(Paths);
    private readonly Dictionary<string, string> _projectText = new(Paths), _rewritten = new(Paths);
    private readonly List<Project> _order = [];
    private readonly GameBuildRequest _request;
    private readonly List<FileStream> _leases = [];
    private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _dotnet, _sdk, _runtime;
    private long _bytes, _toolchainBytes;
    private double _toolchainEnumerationMilliseconds, _toolchainOpenMilliseconds, _toolchainHashMilliseconds;
    private int _entries, _packageEntries;

    private GameBuildPreparation(GameBuildRequest request, string job, string phase, string excluded)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Frozen preparation currently requires Windows file-sharing leases.");
        _job = Path.GetFullPath(job); _directory = Path.Combine(_job, "preparation", phase);
        _request = request;
        _projectDirectory = Path.GetDirectoryName(request.ProjectPath)!; _excluded = Path.GetFullPath(excluded);
        _sourcePolicy = new(_excluded);
        if (Paths.Equals(_projectDirectory, _excluded) || Inside(_projectDirectory, _excluded))
            throw new NotSupportedException("Frozen preparation requires an artifact root outside the source directory's ancestors.");
        _runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var root = Directory.GetParent(_runtime)!.Parent!.Parent!.FullName;
        _dotnet = Path.Combine(root, "dotnet.exe");
        _sdk = SelectSdk(root, _projectDirectory);
        var scope = _projectDirectory;
        for (var ancestor = new DirectoryInfo(_projectDirectory); ancestor != null; ancestor = ancestor.Parent)
            if (new[] { "Directory.Build.props", "Directory.Build.targets", ".editorconfig", ".globalconfig" }.Any(name => ProbeExists(Path.Combine(ancestor.FullName, name)))) scope = ancestor.FullName;
        _roots.Add(Path.EndsInDirectorySeparator(scope) ? scope : scope + Path.DirectorySeparatorChar,
            Path.Combine(_directory, "project") + Path.DirectorySeparatorChar);
        ProjectPath = Mirror(request.ProjectPath);
    }

    internal string ProjectPath { get; }
    internal GameBuildPreparationSnapshot Snapshot { get; private set; } = null!;
    internal IReadOnlyDictionary<string, GameBuildInputFile> Files => _files;
    internal IReadOnlyList<Project> References => _order.Where(project => !Paths.Equals(project.Path, _request.ProjectPath)).ToArray();
    internal sealed class Project(string path, string assembly, string key)
    {
        internal string Path { get; } = path;
        internal string Assembly { get; set; } = assembly;
        internal string Key { get; } = key;
        internal List<Project> References { get; } = [];
        internal Dictionary<string, string> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
    internal string ReferenceOutput(Project project) => Path.Combine(_job, "graph", "output", project.Assembly);

    internal static async Task<GameBuildPreparation> CaptureAsync(GameBuildRequest request, string job, string phase,
        string excluded, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        var capture = new GameBuildPreparation(request, job, phase, excluded);
        try
        {
            await capture.CaptureProjectAsync(request.ProjectPath, token).ConfigureAwait(false);
            var assemblies = new HashSet<string>(Paths) { request.EntryAssembly };
            foreach (var project in capture.References)
                if (!assemblies.Add(project.Assembly) || !(request.Dependencies ?? []).Contains(project.Assembly, Paths))
                    throw new NotSupportedException("Frozen project outputs must be unique and explicitly declared in Dependencies: " + project.Assembly);
            await capture.CapturePackagesAsync(assemblies, token).ConfigureAwait(false);
            await capture.AddTransitiveReferencesAsync(token).ConfigureAwait(false);
            await capture.CopyAsync(typeof(Sessions.IGameSession).Assembly.Location, token).ConfigureAwait(false);
            await capture.CopyAsync(typeof(IFactory<>).Assembly.Location, token).ConfigureAwait(false);
            capture.ConfigureEnvironment();
            var sourceMilliseconds = clock.Elapsed.TotalMilliseconds;
            var toolchainClock = Stopwatch.StartNew();
            var toolchain = await capture.LeaseToolchainAsync(token).ConfigureAwait(false);
            var toolchainMilliseconds = toolchainClock.Elapsed.TotalMilliseconds;
            string Normalize(string value) => value.Replace(capture._directory, "$PREPARATION", StringComparison.OrdinalIgnoreCase)
                .Replace(capture._job, "$JOB", StringComparison.OrdinalIgnoreCase);
            var environment = Hash(JsonSerializer.SerializeToUtf8Bytes(capture._environment.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { p.Key, Value = Normalize(p.Value) })));
            var files = Array.AsReadOnly(capture._files.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray());
            var graph = capture.References.Count == 0 ? null : capture._order.Select(project => new
                { project.Path, project.Assembly, References = project.References.Select(reference => reference.Path).ToArray() }).ToArray();
            var fingerprint = Hash(JsonSerializer.SerializeToUtf8Bytes(new { Files = files, Toolchain = toolchain, Environment = environment, Projects = graph }));
            var sourceWatch = capture._sourceWatchUnavailableReason == null
                ? new GameBuildSourceWatchDescriptor(request.ProjectPath, capture._sourcePolicy, files,
                    capture._graph.Keys.Select(path => Path.GetDirectoryName(path)!), capture._probes.Values, capture._treeFiles) : null;
            capture.Snapshot = new(Path.GetFileName(capture._sdk), Path.GetFileName(capture._runtime), toolchain, environment, fingerprint, files,
                clock.Elapsed.TotalMilliseconds, sourceMilliseconds, toolchainMilliseconds, capture._leases.Count, capture._toolchainBytes,
                capture._toolchainEnumerationMilliseconds, capture._toolchainOpenMilliseconds, capture._toolchainHashMilliseconds,
                sourceWatch, capture._sourceWatchUnavailableReason);
            return capture;
        }
        catch { capture.Dispose(); throw; }
    }

    internal string Original(string value)
    {
        foreach (var pair in _roots)
        {
            value = value.Replace(pair.Value, pair.Key, StringComparison.OrdinalIgnoreCase)
                .Replace(pair.Value.Replace('\\', '/'), pair.Key.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
            value = value.Replace(Path.TrimEndingDirectorySeparator(pair.Value), Path.TrimEndingDirectorySeparator(pair.Key), StringComparison.OrdinalIgnoreCase);
            value = value.Replace(pair.Value[Path.GetPathRoot(pair.Value)!.Length..], pair.Key[Path.GetPathRoot(pair.Key)!.Length..], StringComparison.OrdinalIgnoreCase);
        }
        value = value.Replace(_directory[Path.GetPathRoot(_directory)!.Length..],
            Path.Combine(_job, "preparation", "$PHASE")[Path.GetPathRoot(_job)!.Length..], StringComparison.OrdinalIgnoreCase);
        return value.Replace(_directory, Path.Combine(_job, "preparation", "$PHASE"), StringComparison.OrdinalIgnoreCase)
            .Replace(_directory.Replace('\\', '/'), Path.Combine(_job, "preparation", "$PHASE").Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    internal string CopyPath(string path) => Mirror(path);
    internal bool IsBoundary(string directory) => Paths.Equals(directory, Path.Combine(_job, "preparation"));
    internal IEnumerable<KeyValuePair<string, string>> PathMaps => _roots.Select(p => new KeyValuePair<string, string>(p.Value, p.Key));

    internal ProcessStartInfo BuildInfo(string? project = null)
    {
        project ??= _request.ProjectPath;
        var projectDirectory = Path.GetDirectoryName(project)!;
        var node = _graph[project];
        var work = Paths.Equals(project, _request.ProjectPath) ? Path.Combine(_job, "work") : Path.Combine(_job, "graph", node.Key, "work");
        var info = new ProcessStartInfo(_dotnet) { WorkingDirectory = Path.GetDirectoryName(Mirror(project))!, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--fx-version", Path.GetFileName(_runtime), Path.Combine(_sdk, "MSBuild.dll"), Mirror(project),
            "-restore", "-noAutoResponse", "-verbosity:quiet", "-nodeReuse:false", "-property:Configuration=Release", "-property:TargetFramework=" + GameBuildProfile.TargetFramework,
            "-property:OutputPath=" + Path.Combine(_job, "output") + Path.DirectorySeparatorChar,
            "-property:UseArtifactsOutput=true", "-property:ArtifactsPath=" + work,
            "-property:IncludeProjectNameInArtifactsPaths=false", "-property:BuildProjectReferences=false", "-property:UseSharedCompilation=false",
            "-property:ImportDirectoryPackagesProps=false", "-property:MSBuildEnableWorkloadResolver=false", "-property:NuGetAudit=false",
            "-property:EnableSourceControlManagerQueries=false", "-property:EnableSourceLink=false",
            "-property:RestoreConfigFile=" + Path.Combine(_directory, "NuGet.Config"),
            "-property:RestorePackagesWithLockFile=false", "-property:RestoreLockedMode=false", "-property:NuGetLockFilePath=" + Path.Combine(work, "restore.lock.json"),
            "-property:AetherContractPath=" + Mirror(typeof(Sessions.IGameSession).Assembly.Location),
            "-property:TrueMoonContractsPath=" + Mirror(typeof(IFactory<>).Assembly.Location) }) info.ArgumentList.Add(argument);
        foreach (var name in new[] { "Props", "Targets" })
        {
            var source = AncestorFile(projectDirectory, "Directory.Build." + name.ToLowerInvariant());
            info.ArgumentList.Add("-property:ImportDirectoryBuild" + name + "=" + (source == null ? "false" : "true"));
            if (source != null) info.ArgumentList.Add("-property:DirectoryBuild" + name + "Path=" + Mirror(source));
        }
        info.ArgumentList.Add("-property:ErrorLog=" + Escape(Path.Combine(_job, GameBuildDiagnosticReader.FileName)) + "%2Cversion=2.1");
        ApplyEnvironment(info);
        return info;
    }

    internal void ConfigureCompiler(ProcessStartInfo info)
    {
        info.FileName = _dotnet;
        info.ArgumentList.Insert(1, "--fx-version"); info.ArgumentList.Insert(2, Path.GetFileName(_runtime));
        ApplyEnvironment(info);
    }

    private void ApplyEnvironment(ProcessStartInfo info)
    {
        info.Environment.Clear();
        foreach (var pair in _environment) info.Environment[pair.Key] = pair.Value;
    }

    private void ConfigureEnvironment()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var home = Path.Combine(_directory, "home"); var temp = Path.Combine(_directory, "temp");
        Directory.CreateDirectory(home); Directory.CreateDirectory(temp);
        File.WriteAllText(Path.Combine(_directory, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        File.WriteAllText(Path.Combine(_job, "preparation", ".editorconfig"), "root = true\n");
        _environment["SystemRoot"] = system; _environment["WINDIR"] = system;
        _environment["PATH"] = Path.GetDirectoryName(_dotnet)! + ";" + Path.Combine(system, "System32");
        _environment["TEMP"] = temp; _environment["TMP"] = temp;
        _environment["USERPROFILE"] = home; _environment["APPDATA"] = home; _environment["LOCALAPPDATA"] = home;
        foreach (var name in new[] { "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "CommonProgramFiles", "CommonProgramFiles(x86)", "ProgramData", "NUGET_COMMON_APPLICATION_DATA" })
            _environment[name] = home;
        _environment["DOTNET_ROOT"] = Path.GetDirectoryName(_dotnet)!; _environment["DOTNET_HOST_PATH"] = _dotnet;
        _environment["DOTNET_CLI_HOME"] = home; _environment["NUGET_PACKAGES"] = Path.Combine(home, "packages");
        _environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"; _environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        _environment["DOTNET_MULTILEVEL_LOOKUP"] = "0"; _environment["MSBuildSDKsPath"] = Path.Combine(_sdk, "Sdks");
        _environment["MSBuildEnableWorkloadResolver"] = "false"; _environment["MSBUILDDISABLENODEREUSE"] = "1";
        _environment["MSBuildUserExtensionsPath"] = Path.Combine(home, "extensions");
    }

    private async Task<string> LeaseToolchainAsync(CancellationToken token)
    {
        var enumerationStarted = Stopwatch.GetTimestamp();
        var root = Path.GetDirectoryName(_dotnet)!;
        var paths = new HashSet<string>(Paths) { _dotnet };
        // Installed directories are trusted and stable. Validate each ancestor once per pass,
        // with a fresh pass before opening leases; never retain this census across captures.
        var checkedPaths = new HashSet<string>(Paths);
        var entries = 0;
        foreach (var directory in new[] { _sdk, _runtime, Path.Combine(root, "host", "fxr"), Path.Combine(root, "packs", "Microsoft.NETCore.App.Ref") })
            Collect(directory);
        if (paths.Count > 8192) throw new InvalidDataException("Frozen toolchain exceeds its file budget.");
        _toolchainEnumerationMilliseconds = Stopwatch.GetElapsedTime(enumerationStarted).TotalMilliseconds;
        checkedPaths.Clear();
        var files = new List<GameBuildInputFile>(); long bytes = 0;
        // Leases survive hashing until job cleanup. Disable their individual managed buffers:
        // one capture-owned buffer serves every file, without retaining 64 KiB per lease.
        var buffer = new byte[65536];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var opened = Stopwatch.GetTimestamp();
            RejectToolchainLinks(path);
            var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            _leases.Add(lease); bytes += lease.Length;
            if (lease.Length > GameBuildInputSnapshot.MaximumFileBytes || bytes > 1024L * 1024 * 1024) throw new InvalidDataException("Frozen toolchain exceeds its byte budget.");
            _toolchainOpenMilliseconds += Stopwatch.GetElapsedTime(opened).TotalMilliseconds;
            var hashing = Stopwatch.GetTimestamp();
            long read = 0;
            int count;
            while ((count = await lease.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                hash.AppendData(buffer, 0, count);
                read += count;
            }
            if (read != lease.Length) throw new IOException("Frozen toolchain file length changed while reading.");
            files.Add(new(path, read, Convert.ToHexString(hash.GetHashAndReset())));
            _toolchainHashMilliseconds += Stopwatch.GetElapsedTime(hashing).TotalMilliseconds;
        }
        _toolchainBytes = bytes;
        return Hash(JsonSerializer.SerializeToUtf8Bytes(files));

        void Collect(string directory, int depth = 0)
        {
            token.ThrowIfCancellationRequested(); RejectToolchainLinks(directory);
            if (depth > 64) throw new InvalidDataException("Frozen toolchain exceeds its directory depth limit.");
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested(); RejectToolchainLinks(entry);
                if (++entries > 16384) throw new InvalidDataException("Frozen toolchain exceeds its entry budget.");
                if (Directory.Exists(entry)) Collect(entry, depth + 1); else if (paths.Add(entry) && paths.Count > 8192) throw new InvalidDataException("Frozen toolchain exceeds its file budget.");
            }
        }

        void RejectToolchainLinks(string path)
        {
            for (var current = Path.GetFullPath(path); current != null && !checkedPaths.Contains(current); current = Path.GetDirectoryName(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new NotSupportedException("Frozen preparation requires regular paths without links.");
                checkedPaths.Add(current);
            }
        }
    }

    private async Task CopyDirectoryAsync(string directory, CancellationToken token, int depth = 0)
    {
        token.ThrowIfCancellationRequested(); RejectLinks(directory);
        if (!_copiedDirectories.Add(Path.GetFullPath(directory))) return;
        if (depth > GameBuildSourceWatchPolicy.MaximumDepth) throw new InvalidDataException("Frozen project exceeds its directory depth limit.");
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            token.ThrowIfCancellationRequested();
            if (++_entries > GameBuildSourceWatchPolicy.MaximumEntries) throw new InvalidDataException("Frozen project exceeds its entry budget.");
            if (_sourcePolicy.ExcludesEntry(path, Directory.Exists(path))) continue;
            RejectLinks(path);
            if (Directory.Exists(path)) await CopyDirectoryAsync(path, token, depth + 1).ConfigureAwait(false);
            else
            {
                await CopyAsync(path, token).ConfigureAwait(false);
                _treeFiles.Add(Path.GetFullPath(path));
            }
        }
    }

    private async Task CopyAsync(string path, CancellationToken token)
    {
        path = Path.GetFullPath(path); if (_files.ContainsKey(path)) return;
        token.ThrowIfCancellationRequested(); RejectLinks(path);
        if (_files.Count + _packageEntries >= GameBuildInputSnapshot.MaximumFiles) throw new InvalidDataException("Too many frozen project files.");
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (source.Length > GameBuildInputSnapshot.MaximumFileBytes || _bytes > GameBuildInputSnapshot.MaximumTotalBytes - source.Length)
            throw new InvalidDataException("Frozen project exceeds its byte budget.");
        var target = Mirror(path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using (var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            await source.CopyToAsync(destination, token).ConfigureAwait(false);
        source.Position = 0;
        _files.Add(path, new(path, source.Length, Convert.ToHexString(await SHA256.HashDataAsync(source, token).ConfigureAwait(false))));
        _bytes += source.Length;
    }

    private async Task<Project> CaptureProjectAsync(string path, CancellationToken token)
    {
        path = Path.GetFullPath(path);
        if (_visiting.Contains(path)) throw new NotSupportedException("Cyclic frozen ProjectReference graph.");
        if (_graph.TryGetValue(path, out var existing)) return existing;
        if (_graph.Count >= GameBuildSourceWatchPolicy.MaximumTrees) throw new NotSupportedException("Frozen ProjectReference graph exceeds four projects.");
        if (!path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new NotSupportedException("Frozen ProjectReference requires an existing C# project.");
        if (Inside(path, _excluded)) throw new NotSupportedException("Frozen projects cannot live inside their artifact owner.");
        var projectDirectory = Path.GetDirectoryName(path)!;
        if (_graph.Values.Any(project => Paths.Equals(Path.GetDirectoryName(project.Path), projectDirectory) ||
            Inside(projectDirectory, Path.GetDirectoryName(project.Path)!) || Inside(Path.GetDirectoryName(project.Path)!, projectDirectory)))
            throw new NotSupportedException("Frozen graph projects require distinct non-overlapping source directories.");
        if (!Paths.Equals(SelectSdk(Path.GetDirectoryName(_dotnet)!, Path.GetDirectoryName(path)!), _sdk))
            throw new NotSupportedException("All frozen projects must select the same installed SDK.");
        var project = new Project(path, Paths.Equals(path, _request.ProjectPath) ? _request.EntryAssembly : Path.GetFileNameWithoutExtension(path) + ".dll",
            Hash(Encoding.UTF8.GetBytes(path.ToUpperInvariant()))[..16]);
        _graph.Add(path, project); _visiting.Add(path);
        Mirror(path); // Establish a whole-directory mirror before copying an external project's children.
        await CopyDirectoryAsync(Path.GetDirectoryName(path)!, token).ConfigureAwait(false);
        await ReadProjectAsync(path, true, project, token).ConfigureAwait(false);
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory != null; directory = directory.Parent)
        {
            foreach (var name in new[] { "MSBuild.rsp", "Directory.Build.rsp" })
                if (ProbeExists(Path.Combine(directory.FullName, name))) throw new NotSupportedException("Frozen preparation does not support ambient response files.");
            foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets" })
            {
                var file = Path.Combine(directory.FullName, name);
                if (ProbeExists(file)) await ReadProjectAsync(file, false, project, token).ConfigureAwait(false);
            }
            foreach (var name in new[] { ".editorconfig", ".globalconfig", "global.json" })
            {
                var file = Path.Combine(directory.FullName, name);
                if (ProbeExists(file)) await CopyAsync(file, token).ConfigureAwait(false);
            }
        }
        _visiting.Remove(path); _order.Add(project); return project;
    }

    private async Task AddTransitiveReferencesAsync(CancellationToken token)
    {
        foreach (var project in _order)
        {
            var reachable = new HashSet<Project>(); Visit(project);
            var transitive = _order.Where(reference => reachable.Contains(reference) && !project.References.Contains(reference)).ToArray();
            if (transitive.Length == 0) continue;
            var target = Mirror(project.Path);
            var xml = XDocument.Parse(await File.ReadAllTextAsync(target, token).ConfigureAwait(false), LoadOptions.PreserveWhitespace);
            xml.Root!.Add(new XElement("ItemGroup", transitive.Select(reference => new XElement("Reference",
                new XAttribute("Include", Path.GetFileNameWithoutExtension(reference.Assembly)),
                new XElement("HintPath", ReferenceOutput(reference)), new XElement("Private", "true")))));
            await File.WriteAllTextAsync(target, xml.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false), token).ConfigureAwait(false);
            void Visit(Project current)
            { foreach (var reference in current.References) if (reachable.Add(reference)) Visit(reference); }
        }
    }

    private async Task ReadProjectAsync(string path, bool entry, Project owner, CancellationToken token, int depth = 0, bool conditional = false)
    {
        if (depth > 64) throw new InvalidDataException("Frozen project exceeds its import depth limit.");
        path = Path.GetFullPath(path);
        if (!_projects.Add(owner.Path + "|" + path)) return;
        await CopyAsync(path, token).ConfigureAwait(false);
        if (_files[path].Length > GameBuildInputSnapshot.MaximumDocumentBytes) throw new InvalidDataException("Frozen project XML exceeds its document budget.");
        var target = Mirror(path);
        if (!_projectText.TryGetValue(path, out var text)) _projectText.Add(path, text = await File.ReadAllTextAsync(target, token).ConfigureAwait(false));
        using var reader = XmlReader.Create(new StringReader(text), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = GameBuildInputSnapshot.MaximumDocumentBytes });
        var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        reader.Dispose();
        var root = xml.Root ?? throw new InvalidDataException("Missing project root.");
        if (root.Name != "Project" || root.Attributes().Any(a => a.Name != "Sdk") || (entry ? (string?)root.Attribute("Sdk") != "Microsoft.NET.Sdk" : root.HasAttributes))
            throw new NotSupportedException("Frozen preparation requires plain Microsoft.NET.Sdk projects and declarative imports.");
        if (entry && !Paths.Equals(owner.Path, _request.ProjectPath))
        {
            var names = root.Elements("PropertyGroup").Elements("AssemblyName").ToArray();
            if (names.Length > 1 || names.Any(name => name.HasAttributes || name.Parent!.Attribute("Condition") != null ||
                !Regex.IsMatch(name.Value, @"^[A-Za-z_][A-Za-z0-9_.-]*$")))
                throw new NotSupportedException("Frozen referenced libraries require a literal unconditional AssemblyName.");
            if (names.Length == 1) owner.Assembly = names[0].Value + ".dll";
        }
        foreach (var element in root.DescendantsAndSelf())
        {
            token.ThrowIfCancellationRequested();
            if (element.Name.Namespace != XNamespace.None) throw new NotSupportedException("Namespaced MSBuild XML is unsupported by frozen preparation.");
            foreach (var value in element.Attributes().Select(a => a.Value).Append(element.HasElements ? "" : element.Value))
            {
                if (value.Contains("$([", StringComparison.Ordinal) || value.Contains("%(", StringComparison.Ordinal) || value.Contains("@(", StringComparison.Ordinal))
                    throw new NotSupportedException("MSBuild functions and item expressions are unsupported by frozen preparation.");
                if (Regex.Matches(value, @"\$\(([^)]*)\)").Any(match => !Regex.IsMatch(match.Groups[1].Value, @"^[A-Za-z_][A-Za-z0-9_]*$")))
                    throw new NotSupportedException("Only simple MSBuild property references are supported by frozen preparation.");
            }
            if (element.Attribute("Condition") is { } condition && Regex.IsMatch(Regex.Replace(condition.Value, @"\$\([A-Za-z0-9_]+\)", "value"), @"[A-Za-z]+\s*\("))
                throw new NotSupportedException("MSBuild condition functions are unsupported by frozen preparation.");
        }
        foreach (var element in root.Elements().ToArray())
        {
            switch (element.Name.LocalName)
            {
                case "Import":
                    if (element.Attributes().Any(a => a.Name != "Project" && a.Name != "Condition" && a.Name != "Label"))
                        throw new NotSupportedException("Custom SDK imports are unsupported by frozen preparation.");
                    var import = Resolve((string?)element.Attribute("Project") ?? "", path, owner);
                    if (ProbeExists(import)) await ReadProjectAsync(import, false, owner, token, depth + 1,
                        conditional || element.Attribute("Condition") != null).ConfigureAwait(false);
                    else if (element.Attribute("Condition") == null) throw new FileNotFoundException("Missing frozen import.", import);
                    element.SetAttributeValue("Project", Mirror(import)); break;
                case "PropertyGroup":
                    foreach (var property in element.Elements())
                        if (!Properties.Contains(property.Name.LocalName) || property.HasElements) throw new NotSupportedException("Unsupported frozen MSBuild property: " + property.Name);
                    break;
                case "ItemGroup":
                    foreach (var item in element.Elements().ToArray())
                    {
                        var kind = item.Name.LocalName;
                        if (kind == "ProjectReference" && ((string?)item.Attribute("Condition"))?.Replace(" ", "") == "'$(AetherContractPath)'==''") { item.Remove(); continue; }
                        if (kind == "ProjectReference")
                        {
                            if (!_request.CaptureProjectReferences || conditional || item.AncestorsAndSelf().Any(parent => parent.Attribute("Condition") != null) ||
                                item.Attributes().Any(attribute => attribute.Name != "Include") || item.HasElements || item.Attribute("Include") == null)
                                throw new NotSupportedException("Frozen ProjectReference requires opt-in and a plain unconditional Include without metadata.");
                            var referencePath = Resolve(item.Attribute("Include")!.Value, path, owner, itemPath: true);
                            if (referencePath.IndexOfAny(['*', '?']) >= 0) throw new NotSupportedException("Frozen ProjectReference must be a literal project path.");
                            var referenced = await CaptureProjectAsync(referencePath, token).ConfigureAwait(false);
                            if (!owner.References.Contains(referenced)) owner.References.Add(referenced);
                            item.ReplaceWith(new XElement("Reference", new XAttribute("Include", Path.GetFileNameWithoutExtension(referenced.Assembly)),
                                new XElement("HintPath", ReferenceOutput(referenced)), new XElement("Private", "true")));
                            continue;
                        }
                        if (kind == "PackageReference")
                        {
                            if (_request.PackageDirectory == null || conditional || item.AncestorsAndSelf().Any(parent => parent.Attribute("Condition") != null) ||
                                item.Attributes().Any(attribute => attribute.Name != "Include" && attribute.Name != "Version") || item.HasElements ||
                                item.Attribute("Include") == null || item.Attribute("Version") == null)
                                throw new NotSupportedException("Frozen PackageReference requires PackageDirectory and unconditional literal Include/Version without metadata.");
                            var id = item.Attribute("Include")!.Value; var version = item.Attribute("Version")!.Value;
                            GameBuildPackages.ValidateReference(id, version);
                            if (!owner.Packages.TryAdd(id, version)) throw new NotSupportedException("Duplicate frozen PackageReference: " + id);
                            item.Remove(); continue;
                        }
                        if (kind is not ("Reference" or "Compile" or "None" or "Content" or "AdditionalFiles" or "EditorConfigFiles" or "GlobalAnalyzerConfigFiles" or "EmbeddedResource"))
                            throw new NotSupportedException("Unsupported frozen MSBuild item: " + kind);
                        if (item.Descendants().Any(e => e.HasElements)) throw new NotSupportedException("Nested MSBuild item metadata is unsupported.");
                        if (kind == "Reference")
                        {
                            var hint = item.Element("HintPath");
                            var hintAttribute = item.Attribute("HintPath");
                            if (hint == null && hintAttribute == null || hint != null && hintAttribute != null)
                                throw new NotSupportedException("Frozen references require one explicit HintPath.");
                            var reference = Resolve(hint?.Value ?? hintAttribute!.Value, path, owner); await CopyAsync(reference, token).ConfigureAwait(false);
                            if (hint != null) hint.Value = Mirror(reference); else hintAttribute!.Value = Mirror(reference);
                        }
                        else if (item.Attribute("Include") is { } include)
                        {
                            var input = Resolve(include.Value, path, owner, itemPath: true);
                            if (input.IndexOfAny(['*', '?', ';']) >= 0)
                            {
                                if (!Inside(input, Path.GetDirectoryName(owner.Path)!) || include.Value.Contains(';')) throw new NotSupportedException("Frozen globs must remain inside the project directory.");
                                include.Value = Mirror(input);
                            }
                            else { await CopyAsync(input, token).ConfigureAwait(false); include.Value = Mirror(input); }
                        }
                    }
                    break;
                default: throw new NotSupportedException("Executable/custom MSBuild elements are unsupported by frozen preparation: " + element.Name);
            }
        }
        var rewritten = xml.ToString(SaveOptions.DisableFormatting);
        if (_rewritten.TryGetValue(path, out var previous) && previous != rewritten)
            throw new NotSupportedException("Context-dependent shared imports are unsupported by the frozen project graph.");
        _rewritten[path] = rewritten;
        await File.WriteAllTextAsync(target, rewritten, new UTF8Encoding(false), token).ConfigureAwait(false);
    }

    private async Task CapturePackagesAsync(HashSet<string> assemblies, CancellationToken token)
    {
        if (_order.All(project => project.Packages.Count == 0)) return;
        if (Inside(_request.PackageDirectory!, _excluded) || Paths.Equals(_request.PackageDirectory, _excluded))
            throw new NotSupportedException("PackageDirectory cannot live inside its artifact owner.");
        var packages = new GameBuildPackages(_request.PackageDirectory!, Path.Combine(_directory, "packages"), CopyAsync, Mirror, bytes =>
        {
            if (++_packageEntries + _files.Count > GameBuildInputSnapshot.MaximumFiles || bytes > GameBuildInputSnapshot.MaximumFileBytes ||
                _bytes > GameBuildInputSnapshot.MaximumTotalBytes - bytes) throw new InvalidDataException("Frozen packages exceed the shared source/expanded-asset budget.");
            _bytes += bytes;
        });
        var closures = new Dictionary<Project, IReadOnlyList<GameBuildPackages.Library>>();
        foreach (var project in _order)
        {
            var inherited = project.References.SelectMany(reference => closures[reference]).Distinct().ToArray();
            var libraries = project.Packages.Count == 0 ? inherited : await packages.CaptureAsync(project.Path, project.Packages,
                inherited, _graph.Values.Where(candidate => !Paths.Equals(candidate.Path, project.Path)).Select(candidate => Path.GetFileNameWithoutExtension(candidate.Path)), token).ConfigureAwait(false);
            closures.Add(project, libraries);
            if (libraries.Count == 0) continue;
            var target = Mirror(project.Path);
            var xml = XDocument.Parse(await File.ReadAllTextAsync(target, token).ConfigureAwait(false), LoadOptions.PreserveWhitespace);
            xml.Root!.Add(new XElement("ItemGroup", libraries.Select(library => new XElement("Reference",
                new XAttribute("Include", Path.GetFileNameWithoutExtension(library.Name)), new XElement("HintPath", library.Path), new XElement("Private", "true")))));
            await File.WriteAllTextAsync(target, xml.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false), token).ConfigureAwait(false);
        }
        foreach (var library in packages.Libraries)
            if (!assemblies.Add(library.Name) || !(_request.Dependencies ?? []).Contains(library.Name, Paths))
                throw new NotSupportedException("Frozen package DLLs must be unique and explicitly declared in Dependencies: " + library.Name);
        GameBuildArtifact.ValidateNames(assemblies.ToArray());
    }

    private string Resolve(string value, string project, Project owner, bool itemPath = false)
    {
        value = value.Replace("$(MSBuildThisFileDirectory)", Path.GetDirectoryName(project)! + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            .Replace("$(MSBuildProjectDirectory)", Path.GetDirectoryName(owner.Path)!, StringComparison.Ordinal)
            .Replace("$(AetherContractPath)", typeof(Sessions.IGameSession).Assembly.Location, StringComparison.Ordinal)
            .Replace("$(TrueMoonContractsPath)", typeof(IFactory<>).Assembly.Location, StringComparison.Ordinal);
        if (value.Contains("$(", StringComparison.Ordinal) || value.IndexOfAny(['\r', '\n', '\0', ';']) >= 0 || string.IsNullOrWhiteSpace(value))
            throw new NotSupportedException("Frozen paths require literal paths or supported path properties.");
        return Path.GetFullPath(value, itemPath ? Path.GetDirectoryName(owner.Path)! : Path.GetDirectoryName(project)!);
    }

    private string Mirror(string path)
    {
        var full = Path.GetFullPath(path);
        foreach (var pair in _roots.OrderByDescending(p => p.Key.Length))
            if (full.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase)) return Path.Combine(pair.Value, full[pair.Key.Length..]);
        var root = Path.GetDirectoryName(full)!;
        if (!Path.EndsInDirectorySeparator(root)) root += Path.DirectorySeparatorChar;
        var copy = Path.Combine(_directory, "external", Hash(Encoding.UTF8.GetBytes(root.ToUpperInvariant()))[..16]) + Path.DirectorySeparatorChar;
        _roots.Add(root, copy);
        return Path.Combine(copy, Path.GetFileName(full));
    }

    private string SelectSdk(string dotnetRoot, string project)
    {
        var minimum = new Version(GameBuildProfile.SdkMajor, 0, 0); var policy = "latestMinor";
        for (var directory = new DirectoryInfo(project); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "global.json"); if (!ProbeExists(path)) continue;
            RejectLinks(path);
            if (new FileInfo(path).Length > GameBuildInputSnapshot.MaximumDocumentBytes) throw new InvalidDataException("global.json exceeds its document limit.");
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var sdk = json.RootElement.GetProperty("sdk");
            policy = sdk.TryGetProperty("rollForward", out var roll) ? roll.GetString()! : "patch";
            if (!Version.TryParse(sdk.GetProperty("version").GetString(), out var requested) || requested.Major != GameBuildProfile.SdkMajor ||
                sdk.TryGetProperty("paths", out _) || sdk.TryGetProperty("allowPrerelease", out var prerelease) && prerelease.GetBoolean()
                || policy is not ("disable" or "patch" or "latestPatch" or "latestMinor"))
                throw new NotSupportedException($"Frozen {GameBuildProfile.TargetFramework} builds require a stable SDK{GameBuildProfile.SdkMajor} with a supported selection policy. Check global.json and rebuild.");
            minimum = requested;
            break;
        }
        var candidates = Directory.EnumerateDirectories(Path.Combine(dotnetRoot, "sdk"))
            .Select(path => (Path: path, Parsed: Version.TryParse(Path.GetFileName(path), out var version), Version: version))
            .Where(v => v.Parsed && v.Version!.Major == GameBuildProfile.SdkMajor && v.Version >= minimum)
            .Where(v => policy == "latestMinor" || policy == "disable" && v.Version == minimum || policy is "patch" or "latestPatch" && v.Version!.Minor == minimum.Minor && v.Version.Build / 100 == minimum.Build / 100)
            .OrderBy(v => v.Version).ToArray();
        return (policy == "patch" && candidates.Any(v => v.Version == minimum) ? candidates.First(v => v.Version == minimum).Path : candidates.LastOrDefault().Path)
            ?? throw new NotSupportedException($"No installed SDK matches the frozen {GameBuildProfile.TargetFramework} profile.");
    }

    private bool ProbeExists(string path)
    {
        // Retain the preparation profile's File.Exists behavior. Projection failures must not invalidate a build.
        var exists = File.Exists(path);
        if (_sourceWatchUnavailableReason != null) return exists;
        path = Path.GetFullPath(path);
        if (!_probes.ContainsKey(path) && _probes.Count >= GameBuildSourceWatchPolicy.MaximumPaths)
        {
            _sourceWatchUnavailableReason = "Source watch exceeds its exact-path probe budget.";
            return exists;
        }
        try
        {
            bool observed;
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw new IOException("Probe is not a regular file path.");
                observed = true;
            }
            catch (FileNotFoundException) { observed = false; }
            catch (DirectoryNotFoundException) { observed = false; }
            if (observed != exists || _probes.TryGetValue(path, out var previous) && previous.Exists != exists)
                throw new IOException("Probe presence changed during capture.");
            _probes[path] = new(path, exists);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _sourceWatchUnavailableReason = "Source watch could not establish probe presence: " + (path.Length <= 512 ? path : path[..512]);
        }
        return exists;
    }

    private static string? AncestorFile(string project, string name)
    {
        for (var directory = new DirectoryInfo(project); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Frozen preparation requires regular paths without links.");
    }
    private static bool Inside(string path, string directory) => path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Escape(string value)
    {
        foreach (var character in new[] { '%', ';', ',', '$', '@', '(', ')', '\'' }) value = value.Replace(character.ToString(), "%" + ((int)character).ToString("X2"), StringComparison.Ordinal);
        return value;
    }
    public void Dispose() { foreach (var lease in _leases) lease.Dispose(); _leases.Clear(); }
}
