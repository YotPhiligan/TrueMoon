using TrueMoon.Aether.Scenes;

namespace AetherGuiConsumer;

// App-owned injected fixture over the real codec/store. A failed close must retain the document.
internal sealed class SceneWriteProbe(JsonSceneDocumentStore store) : ISceneDocumentStore
{
    private readonly JsonSceneDocumentStore _store = store;
    private int _failNext;
    internal void FailNext() => Interlocked.Exchange(ref _failNext, 1);
    public Task<SceneFile> ReadAsync(string path, CancellationToken cancellationToken = default)
        => _store.ReadAsync(path, cancellationToken);
    public Task<SceneFileStamp> WriteAsync(string path, SceneDocument document, SceneFileStamp? expected, CancellationToken cancellationToken = default)
        => Interlocked.Exchange(ref _failNext, 0) != 0
            ? Task.FromException<SceneFileStamp>(new IOException("Acceptance: failed save during close"))
            : _store.WriteAsync(path, document, expected, cancellationToken);
}
