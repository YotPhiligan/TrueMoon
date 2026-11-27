using TrueMoon.Argentis;
using TrueMoon.Argentis.Layout;
using TrueMoon.Alloy.Runtime;

namespace TrueMoon.Argentis.Benchmarks;

/// <summary>
/// The same rows as the keyed-binding group, but realized through a <see cref="VirtualizingStack"/> inside a
/// <see cref="ScrollViewer"/>: only the window plus its buffer exists, so the cost of a source notification or a
/// scroll step must stay flat while the list grows. Read it against keyed-binding, where every case is per row.
/// </summary>
internal abstract class VirtualizedListCase(int rows) : IUiCase
{
    /// <summary>Rows the 2000-unit headless viewport shows; larger scenes must keep the rest unrealized.</summary>
    private const int WindowedAbove = 100;

    protected List<RowModel> Source = null!;
    protected ScrollViewer Scroll = null!;
    protected VirtualizingStack List = null!;
    protected UiSession Session = null!;

    public void Setup()
    {
        Source = Scene.PlainModels(rows);
        List = new VirtualizingStack().BindVirtualizedItems(Source, Scene.BoundRow, static row => row.Id, Rebind);
        Scroll = new ScrollViewer();
        Scroll.SetContent(List);
        Session = Scene.Session(Scroll);
        Session.Update();
        Session.Update();
        if (List.Items.Count > rows) throw new InvalidOperationException("The window realizes more rows than the source has.");
        if (rows > WindowedAbove && List.Items.Count >= rows) throw new InvalidOperationException("The window realizes the whole list.");
    }

    protected static void Rebind(Element row, RowModel model) => ((Text)((HStack)row).Items[0]).Value = model.Name;

    public abstract void Run();

    public void Cleanup() => Session.Dispose();
}

internal sealed class VirtualizedUpdateStatic(int rows) : VirtualizedListCase(rows)
{
    public override void Run() => Session.Update();
}

internal sealed class VirtualizedRefreshUnchanged(int rows) : VirtualizedListCase(rows)
{
    public override void Run()
    {
        List.RefreshItems();
        Session.Update();
    }
}

internal sealed class VirtualizedAppendRemoveRow(int rows) : VirtualizedListCase(rows)
{
    private bool _appended;

    public override void Run()
    {
        if (_appended) Source.RemoveAt(Source.Count - 1);
        else Source.Add(new RowModel(rows));
        _appended = !_appended;
        List.RefreshItems();
        Session.Update();
    }
}

internal sealed class VirtualizedScrollStep(int rows) : VirtualizedListCase(rows)
{
    private bool _moved;

    public override void Run()
    {
        _moved = !_moved;
        Scroll.Offset = _moved ? 400 : 0;
        Session.Update();
    }
}

/// <summary>Build plus the first two layouts: the pair that shows how many rows a scene realizes at all.</summary>
internal sealed class VirtualizedBuildScene(int rows) : IUiCase
{
    private UiSession? _session;

    public void Setup() { }

    public void Run()
    {
        var source = Scene.PlainModels(rows);
        var list = new VirtualizingStack().BindVirtualizedItems(source, Scene.BoundRow, static row => row.Id);
        var scroll = new ScrollViewer();
        scroll.SetContent(list);
        _session = Scene.Session(scroll);
        _session.Update();
        _session.Update();
    }

    public void Cleanup() => _session?.Dispose();
}

/// <summary>The same scene without virtualization, kept as the explicit comparison inside this group.</summary>
internal sealed class VirtualizedBuildSceneNonVirtualized(int rows) : IUiCase
{
    private UiSession? _session;

    public void Setup() { }

    public void Run()
    {
        var source = Scene.PlainModels(rows);
        var list = new VStack().BindItems(source, Scene.BoundRow, static row => row.Id);
        var scroll = new ScrollViewer();
        scroll.SetContent(list);
        _session = Scene.Session(scroll);
        _session.Update();
    }

    public void Cleanup() => _session?.Dispose();
}

/// <summary>
/// The same scroll step over a list that is not virtualized: every row is a child, so the pass re-arranges the whole
/// list. This is the floor the group above is measured against.
/// </summary>
internal sealed class VirtualizedScrollStepNonVirtualized(int rows) : IUiCase
{
    private ScrollViewer _scroll = null!;
    private UiSession _session = null!;
    private bool _moved;

    public void Setup()
    {
        var source = Scene.PlainModels(rows);
        var list = new VStack().BindItems(source, Scene.BoundRow, static row => row.Id);
        _scroll = new ScrollViewer();
        _scroll.SetContent(list);
        _session = Scene.Session(_scroll);
        _session.Update();
    }

    public void Run()
    {
        _moved = !_moved;
        _scroll.Offset = _moved ? 400 : 0;
        _session.Update();
    }

    public void Cleanup() => _session.Dispose();
}
