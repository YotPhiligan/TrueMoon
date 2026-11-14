using System.Reflection;
using System.Runtime.Loader;
using TrueMoon.Aether.Abstractions.Builds;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Runtime.Builds;

namespace TrueMoon.Aether.Runtime;

/// <summary>Loads one validated game in an owned child or standalone local Player, sharing the existing Aether contract assembly.</summary>
/// <remarks>This is dependency isolation for trusted local code, not a security sandbox or a live replacement API.</remarks>
public sealed class GameBuildLoader : IDisposable
{
    private LoadContext? _context;
    private readonly IDisposable _pin;
    private IGameSession? _session;
    private bool _disposed, _disposing;
    private GameBuildLoader(GameBuildArtifact artifact, IFactory<IGameSession, Type> sessions)
    {
        Artifact = artifact; _pin = artifact.Pin(); _context = new(artifact);
        try
        {
            var assembly = _context.LoadFromAssemblyPath(Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly));
            var type = assembly.GetType(artifact.EntryType, throwOnError: true)!;
            if (!type.IsVisible || type.IsAbstract || !typeof(IGameSession).IsAssignableFrom(type))
                throw new InvalidDataException("Game entry must implement the shared IGameSession contract.");
            _session = sessions.Create(type) ?? throw new InvalidDataException("Game session factory returned null.");
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
    /// <param name="build">Selected immutable artifact.</param><param name="hostContractId">Expected host identity.</param>
    /// <param name="sessions">Runtime factory; null uses the explicit game entry factory without App services.</param>
    /// <returns>An owner for the game, collectible load context and artifact pin.</returns>
    public static GameBuildLoader Load(GameBuildReference build, string hostContractId, IFactory<IGameSession, Type>? sessions = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new(GameBuildArtifact.Open(build.DirectoryPath, hostContractId, build.BuildId), sessions ?? new GameSessionFactory(null));
    }
    /// <summary>Disposes the game, requests cooperative context unload and releases artifact pins.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_disposing) throw new InvalidOperationException("Game cleanup is not reentrant.");
        _disposing = true;
        try
        {
            // A failed game/scene cleanup retains its owner, dependency context and artifact pin for retry.
            _session?.Dispose(); _session = null;
            _context?.Unload(); _context = null;
            _pin.Dispose(); _disposed = true;
        }
        finally { _disposing = false; }
    }
    private sealed class LoadContext(GameBuildArtifact artifact) : AssemblyLoadContext("Aether.Game." + artifact.BuildId, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            var shared = artifact.SharedAssemblies.SingleOrDefault(assembly => string.Equals(assembly.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
            if (shared != null)
            {
                if (shared.GetName().FullName != name.FullName) throw new FileLoadException("Shared game contract identity mismatch: " + name.FullName);
                return shared;
            }
            var file = artifact.Files.SingleOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), name.Name, StringComparison.OrdinalIgnoreCase));
            if (file != null) return LoadFromAssemblyPath(Path.Combine(artifact.DirectoryPath, file));
            if (name.Name != null && GameBuildArtifact.FrameworkNames.Contains(name.Name)) return null;
            throw new FileLoadException("Game dependency is not declared by its artifact: " + name.FullName);
        }
        protected override nint LoadUnmanagedDll(string name) => throw new NotSupportedException("Native game dependencies are outside ENG3a: " + name);
    }
}
