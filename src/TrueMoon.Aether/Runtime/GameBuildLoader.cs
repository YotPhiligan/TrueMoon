using System.Reflection;
using System.Runtime.Loader;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Runtime;

/// <summary>Loads one validated game in the child runtime, sharing the existing Aether contract assembly.</summary>
/// <remarks>This is dependency isolation for trusted local code, not a security sandbox or a live replacement API.</remarks>
public sealed class GameBuildLoader : IDisposable
{
    private LoadContext? _context;
    private readonly IDisposable _pin;
    private IGameSession? _session;
    private bool _disposed;
    private GameBuildLoader(GameBuildArtifact artifact)
    {
        Artifact = artifact; _pin = artifact.Pin(); _context = new(artifact);
        try
        {
            var assembly = _context.LoadFromAssemblyPath(Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly));
            var type = assembly.GetType(artifact.EntryType, throwOnError: true)!;
            if (!type.IsVisible || type.IsAbstract || !typeof(IGameSession).IsAssignableFrom(type))
                throw new InvalidDataException("Game entry must implement the shared IGameSession contract.");
            _session = (IGameSession)(Activator.CreateInstance(type) ?? throw new InvalidDataException("Game entry construction returned null."));
            LoadedAssemblyPath = assembly.Location;
        }
        catch { _context.Unload(); _context = null; _pin.Dispose(); throw; }
    }
    /// <summary>Gets the pinned artifact.</summary>
    public GameBuildArtifact Artifact { get; }
    /// <summary>Gets the child-owned game instance.</summary>
    public IGameSession Session => _session ?? throw new ObjectDisposedException(nameof(GameBuildLoader));
    /// <summary>Gets the actual loaded entry assembly path for provenance diagnostics.</summary>
    public string LoadedAssemblyPath { get; }
    /// <summary>Validates and loads a selected artifact. Call only inside the game child.</summary>
    public static GameBuildLoader Load(GameBuildReference build, string hostContractId)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new(GameBuildArtifact.Open(build.DirectoryPath, hostContractId, build.BuildId));
    }
    /// <summary>Disposes the game, requests cooperative context unload and releases artifact pins.</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        var session = _session; _session = null; var context = _context; _context = null;
        try { session?.Dispose(); } finally { try { context?.Unload(); } finally { _pin.Dispose(); } }
    }
    private sealed class LoadContext(GameBuildArtifact artifact) : AssemblyLoadContext("Aether.Game." + artifact.BuildId, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == typeof(IGameSession).Assembly.GetName().Name) return typeof(IGameSession).Assembly;
            var file = artifact.Files.SingleOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), name.Name, StringComparison.OrdinalIgnoreCase));
            if (file != null) return LoadFromAssemblyPath(Path.Combine(artifact.DirectoryPath, file));
            if (name.Name != null && GameBuildArtifact.FrameworkNames.Contains(name.Name)) return null;
            throw new FileLoadException("Game dependency is not declared by its artifact: " + name.FullName);
        }
        protected override nint LoadUnmanagedDll(string name) => throw new NotSupportedException("Native game dependencies are outside ENG3a: " + name);
    }
}
