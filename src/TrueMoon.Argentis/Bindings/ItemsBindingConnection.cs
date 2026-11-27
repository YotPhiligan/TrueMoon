using System.Collections.Specialized;

namespace TrueMoon.Argentis.Bindings;

/// <summary>
/// Attachment-scoped queue shared by item bindings: source notifications coalesce into one queued
/// synchronization, and a synchronization is dropped when the attachment ended before it ran.
/// </summary>
internal sealed class ItemsBindingConnection(Element element, INotifyCollectionChanged? source, Action synchronize) : IDisposable
{
    private int _active = 1;
    private int _pending;
    private int _version;
    private bool _subscribed;
    private readonly Action<Action> _dispatch = element.CaptureAttachmentDispatcher();
    internal bool Active => Volatile.Read(ref _active) != 0 && element.IsAttached;
    internal int Version => Volatile.Read(ref _version);

    internal void Connect()
    {
        if (source != null)
        {
            _subscribed = true;
            source.CollectionChanged += Changed;
        }

        Request();
    }

    private void Changed(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (!Active) return;
        Interlocked.Increment(ref _version);
        Request();
    }

    internal void Request()
    {
        if (!Active || Interlocked.Exchange(ref _pending, 1) != 0) return;
        try
        {
            _dispatch(() =>
            {
                Interlocked.Exchange(ref _pending, 0);
                if (Active) synchronize();
            });
        }
        catch (ObjectDisposedException) when (!Active || element.IsDisposed)
        {
            Interlocked.Exchange(ref _pending, 0);
        }
        catch
        {
            Interlocked.Exchange(ref _pending, 0);
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _active, 0) == 0) return;
        if (_subscribed) source!.CollectionChanged -= Changed;
    }
}
