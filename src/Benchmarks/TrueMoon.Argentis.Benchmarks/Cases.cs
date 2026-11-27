using TrueMoon.Argentis;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis.Views;
using System.Collections.ObjectModel;
using System.Collections.Immutable;

namespace TrueMoon.Argentis.Benchmarks;

/// <summary>One measured View-layer operation with its own scene lifetime.</summary>
internal interface IUiCase
{
    void Setup();
    void Run();
    void Cleanup();
}
internal sealed record CaseDescriptor(string Group, string Method, Func<int, IUiCase> Factory);

/// <summary>The permanent UIA4.2 case set. Every group also measures <c>NoOp</c> as its harness floor.</summary>
internal static class Cases
{
    internal static readonly string[] Groups = ["node-cost", "view-node", "property-access", "property-store", "collection-binding", "keyed-binding", "virtualized-binding", "property-binding", "conditional-slot", "tree-sync"];

    /// <summary>Scene sizes per group: rows for the scene and binding groups, nodes for <c>property-store</c>, binding
    /// count for <c>property-binding</c>.</summary>
    internal static int[] Sizes(string group) => group switch
    {
        "property-store" => [39, 312, 2496],
        "property-binding" => [1, 64, 512],
        _ => [39, 312, 2496],
    };

    internal static IReadOnlyList<CaseDescriptor> For(string group) => group switch
    {
        "node-cost" => [.. NodeCost(), new CaseDescriptor("node-cost", "NoOp", static _ => new NullCase())],
        "view-node" => [.. ViewNode(), new CaseDescriptor("view-node", "NoOp", static _ => new NullCase())],
        "property-access" => [.. PropertyAccess(), new CaseDescriptor("property-access", "NoOp", static _ => new NullCase())],
        "property-store" => [.. PropertyStore(), new CaseDescriptor("property-store", "NoOp", static _ => new NullCase())],
        "collection-binding" => [.. CollectionBinding(), new CaseDescriptor("collection-binding", "NoOp", static _ => new NullCase())],
        "keyed-binding" => [.. KeyedBinding(), new CaseDescriptor("keyed-binding", "NoOp", static _ => new NullCase())],
        "virtualized-binding" => [.. VirtualizedBinding(), new CaseDescriptor("virtualized-binding", "NoOp", static _ => new NullCase())],
        "property-binding" => [.. PropertyBinding(), new CaseDescriptor("property-binding", "NoOp", static _ => new NullCase())],
        "conditional-slot" => [.. ConditionalSlot(), new CaseDescriptor("conditional-slot", "NoOp", static _ => new NullCase())],
        "tree-sync" => [.. TreeSync(), new CaseDescriptor("tree-sync", "NoOp", static _ => new NullCase())],
        _ => throw new ArgumentOutOfRangeException(nameof(group)),
    };

    private static IEnumerable<CaseDescriptor> NodeCost()
    {
        yield return new("node-cost", "BuildScene", size => new BuildScene(size));
        yield return new("node-cost", "BuildAndDisposeScene", size => new BuildAndDisposeScene(size));
        yield return new("node-cost", "MeasureScene", size => new MeasureScene(size));
        yield return new("node-cost", "ArrangeScene", size => new ArrangeScene(size));
        yield return new("node-cost", "MeasureAndArrangeScene", size => new MeasureAndArrangeScene(size));
        yield return new("node-cost", "LeafEditThenLayout", size => new LeafEditThenLayout(size));
    }

    // Sizes are rows, and every row is one View rather than one plain panel.
    private static IEnumerable<CaseDescriptor> ViewNode()
    {
        yield return new("view-node", "BuildViewScene", size => new BuildViewScene(size));
        yield return new("view-node", "RelayoutViewScene", size => new RelayoutViewScene(size));
    }

    private static IEnumerable<CaseDescriptor> PropertyAccess()
    {
        yield return new("property-access", "GetLocalValue", size => new GetLocalValue(size));
        yield return new("property-access", "GetThemeValue", size => new GetThemeValue(size));
        yield return new("property-access", "GetUnknownDefault", size => new GetUnknownDefault(size));
        yield return new("property-access", "SetSameValue", size => new SetSameValue(size));
        yield return new("property-access", "SetChangedValue", size => new SetChangedValue(size));
        yield return new("property-access", "InvalidateRender", size => new InvalidateRender(size));
    }

    private static IEnumerable<CaseDescriptor> CollectionBinding()
    {
        yield return new("collection-binding", "UpdateStatic", size => new UpdateStatic(size));
        yield return new("collection-binding", "AppendRemoveRow", size => new AppendRemoveRow(size));
        yield return new("collection-binding", "MoveFirstRowToEnd", size => new MoveFirstRowToEnd(size));
        yield return new("collection-binding", "NotifyOneRowName", size => new NotifyOneRowName(size));
        yield return new("collection-binding", "RefreshAllRows", size => new RefreshAllRows(size));
        yield return new("collection-binding", "ResetAllRows", size => new ResetAllRows(size));
    }

    private static IEnumerable<CaseDescriptor> KeyedBinding()
    {
        yield return new("keyed-binding", "KeyedUpdateStatic", size => new KeyedUpdateStatic(size));
        yield return new("keyed-binding", "KeyedRefreshUnchanged", size => new KeyedRefreshUnchanged(size));
        yield return new("keyed-binding", "KeyedAppendRemoveRow", size => new KeyedAppendRemoveRow(size));
        yield return new("keyed-binding", "KeyedMoveFirstRowToEnd", size => new KeyedMoveFirstRowToEnd(size));
        yield return new("keyed-binding", "KeyedRebuildSameKeys", size => new KeyedRebuildSameKeys(size));
        yield return new("keyed-binding", "KeyedReplaceRowInstance", size => new KeyedReplaceRowInstance(size));
    }

    private static IEnumerable<CaseDescriptor> VirtualizedBinding()
    {
        yield return new("virtualized-binding", "BuildScene", size => new VirtualizedBuildScene(size));
        yield return new("virtualized-binding", "BuildSceneNonVirtualized", size => new VirtualizedBuildSceneNonVirtualized(size));
        yield return new("virtualized-binding", "UpdateStatic", size => new VirtualizedUpdateStatic(size));
        yield return new("virtualized-binding", "RefreshUnchanged", size => new VirtualizedRefreshUnchanged(size));
        yield return new("virtualized-binding", "AppendRemoveRow", size => new VirtualizedAppendRemoveRow(size));
        yield return new("virtualized-binding", "ScrollStep", size => new VirtualizedScrollStep(size));
        yield return new("virtualized-binding", "ScrollStepNonVirtualized", size => new VirtualizedScrollStepNonVirtualized(size));
    }

    private static IEnumerable<CaseDescriptor> PropertyBinding()
    {
        yield return new("property-binding", "CreateBindings", size => new CreateBindings(size));
        yield return new("property-binding", "AttachAndFirstSync", size => new AttachAndFirstSync(size));
        yield return new("property-binding", "NotifyOneAndUpdate", size => new NotifyOneAndUpdate(size));
        yield return new("property-binding", "NotifyAllAndUpdate", size => new NotifyAllAndUpdate(size));
        yield return new("property-binding", "UiEditWritesSource", size => new UiEditWritesSource(size));
    }

    // Sizes are nodes for this group: one detached element per unit, not an inspector row.
    private static IEnumerable<CaseDescriptor> PropertyStore()
    {
        yield return new("property-store", "StoreOneProperty", size => new StoreOneProperty(size));
        yield return new("property-store", "StoreTwoProperties", size => new StoreTwoProperties(size));
        yield return new("property-store", "StoreThreeProperties", size => new StoreThreeProperties(size));
    }

    // Sizes are rows inside the conditional branch, not nodes of the surrounding scene.
    private static IEnumerable<CaseDescriptor> ConditionalSlot()
    {
        yield return new("conditional-slot", "ManualSetContentAndDispose", size => new ManualSetContentAndDispose(size));
        yield return new("conditional-slot", "SlotNewState", size => new SlotNewState(size));
        yield return new("conditional-slot", "SlotSameState", size => new SlotSameState(size));
        yield return new("conditional-slot", "SlotCloseAndReopen", size => new SlotCloseAndReopen(size));
        yield return new("conditional-slot", "SlotFreshEqualToken", size => new SlotFreshEqualToken(size));
    }

    // Sizes are rows: every row contributes one model notification, and the structural pass reconciles them once.
    private static IEnumerable<CaseDescriptor> TreeSync()
    {
        yield return new("tree-sync", "ManualMaskQueue", size => new ManualMaskQueue(size));
        yield return new("tree-sync", "DeferredTreeSyncQueue", size => new DeferredTreeSyncQueue(size));
    }
}

/// <summary>The branch both conditional cases build: one label per available choice.</summary>
internal static class ConditionalBranch
{
    public static VStack Build(int rows, int token)
    {
        var branch = new VStack();
        for (var index = 0; index < rows; index++) branch.Add(new Text("choice-" + token.ToStringInvariant() + "-" + index.ToStringInvariant()));
        return branch;
    }
}

/// <summary>The application pattern before UIA4.5: build a branch by hand, hand it to the host, dispose the previous one.</summary>
internal sealed class ManualSetContentAndDispose(int rows) : IUiCase
{
    private int _token;
    private ScrollViewer _host = null!;
    private UiSession _session = null!;

    public void Setup()
    {
        _host = new ScrollViewer();
        _session = Scene.Session(_host);
        _session.Update();
    }

    public void Run()
    {
        var branch = ConditionalBranch.Build(rows, _token);
        var previous = _host.Child;
        _host.SetContent(branch);
        previous?.Dispose();
        _token++;
        _session.Update();
    }

    public void Cleanup() => _session.Dispose();
}

/// <summary>The identity of one requested branch; equal values must keep the installed subtree.</summary>
internal sealed record ConditionalState(int Token);

internal abstract class ConditionalSlotCase(int rows) : IUiCase
{
    protected int Token;
    protected ScrollViewer Host = null!;
    protected ConditionalContent<ConditionalState?> Slot = null!;
    protected UiSession Session = null!;

    public void Setup()
    {
        Host = new ScrollViewer();
        Slot = new ConditionalContent<ConditionalState?>(new ConditionalState(0), state => state == null ? null : ConditionalBranch.Build(rows, state.Token));
        Host.SetContent(Slot);
        Session = Scene.Session(Host);
        Session.Update();
        if (!Slot.HasBranch) throw new InvalidOperationException("The initial conditional branch is missing.");
    }

    public abstract void Run();

    public void Cleanup() => Session.Dispose();
}

internal sealed class SlotNewState(int rows) : ConditionalSlotCase(rows)
{
    public override void Run()
    {
        Slot.State = new ConditionalState(++Token);
        Session.Update();
    }
}

internal sealed class SlotSameState(int rows) : ConditionalSlotCase(rows)
{
    public override void Run()
    {
        // The same requested state: the slot keeps the installed rows, only the request itself is allocated.
        Slot.State = new ConditionalState(Token);
        Session.Update();
    }
}

internal sealed class SlotCloseAndReopen(int rows) : ConditionalSlotCase(rows)
{
    public override void Run()
    {
        Slot.State = null;
        Session.Update();
        Slot.State = new ConditionalState(++Token);
        Session.Update();
    }
}

/// <summary>A property block token as the Editor inspector builds it: parallel key and value arrays that are rebuilt
/// on every model notification and stay equal when the presented block did not change. <c>ImmutableArray</c> itself
/// compares by reference, so the token compares its own contents.</summary>
internal sealed record PropertyBlockToken(ImmutableArray<string> Keys, ImmutableArray<string> Values)
{
    public bool Equals(PropertyBlockToken? other) => other != null &&
        Keys.AsSpan().SequenceEqual(other.Keys.AsSpan()) && Values.AsSpan().SequenceEqual(other.Values.AsSpan());

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var key in Keys) hash.Add(key);
        foreach (var value in Values) hash.Add(value);
        return hash.ToHashCode();
    }
}

internal sealed class SlotFreshEqualToken(int rows) : IUiCase
{
    private string[] _keys = [], _values = [];
    private UiSession _session = null!;
    private ConditionalContent<PropertyBlockToken> _slot = null!;

    public void Setup()
    {
        _keys = new string[rows];
        _values = new string[rows];
        for (var index = 0; index < rows; index++)
        {
            _keys[index] = $"property-{index}";
            _values[index] = index.ToString("D");
        }

        _slot = new ConditionalContent<PropertyBlockToken>(Token(), state =>
        {
            var block = new VStack();
            for (var index = 0; index < state.Keys.Length; index++)
                block.Add(new Text($"{state.Keys[index]}={state.Values[index]}"));
            return block;
        });
        _session = Scene.Session(_slot);
        _session.Update();
        if (!_slot.HasBranch) throw new InvalidOperationException("The initial property block is missing.");
    }

    public void Run()
    {
        // A notification whose block did not change: only the token is allocated, the installed rows stay.
        _slot.State = Token();
        _session.Update();
    }

    public void Cleanup() => _session.Dispose();

    private PropertyBlockToken Token() => new([.. _keys], [.. _values]);
}

internal sealed class NullCase : IUiCase
{
    public void Setup() { }
    public void Run() { }
    public void Cleanup() { }
}

internal abstract class TreeCase(int rows) : IUiCase
{
    protected readonly FixedTextLayout Text = new();
    protected VStack Root = null!;

    protected virtual bool PrepareTree => true;

    public virtual void Setup()
    {
        if (PrepareTree) Root = Scene.Inspector(rows);
    }

    public abstract void Run();

    public virtual void Cleanup() => Root?.Dispose();
}

internal sealed class BuildScene(int rows) : TreeCase(rows)
{
    private object? _sink;
    protected override bool PrepareTree => false;
    public override void Run() => _sink = Scene.Inspector(rows);
    public override void Cleanup() { }
}

internal sealed class BuildAndDisposeScene(int rows) : TreeCase(rows)
{
    private bool _sink;
    protected override bool PrepareTree => false;
    public override void Run()
    {
        var tree = Scene.Inspector(rows);
        tree.Dispose();
        _sink = tree.IsDisposed;
    }
    public override void Cleanup() { }
}

internal sealed class MeasureScene(int rows) : TreeCase(rows)
{
    public override void Run() => Root.Measure(Scene.Available, Text);
}

internal sealed class ArrangeScene(int rows) : TreeCase(rows)
{
    public override void Setup()
    {
        base.Setup();
        Root.Measure(Scene.Available, Text);
    }
    public override void Run() => Root.Arrange(Scene.Slot);
}

internal sealed class MeasureAndArrangeScene(int rows) : TreeCase(rows)
{
    public override void Run()
    {
        Root.Measure(Scene.Available, Text);
        Root.Arrange(Scene.Slot);
    }
}

internal sealed class LeafEditThenLayout(int rows) : TreeCase(rows)
{
    private Text _leaf = null!;
    private bool _alternate;

    public override void Setup()
    {
        base.Setup();
        _leaf = (Text)((HStack)Root.Items[^1]).Items[0];
    }

    public override void Run()
    {
        _leaf.Value = _alternate ? "edit-a" : "edit-b";
        _alternate = !_alternate;
        Root.Measure(Scene.Available, Text);
        Root.Arrange(Scene.Slot);
    }
}

/// <summary>
/// The permanent-cost scene of a View node: every row is a <see cref="BenchView"/> whose content is declared
/// once in the constructor and prepared at attach through the session factory registry — the application path
/// an editor screen takes. The <c>node-cost</c> group contains no View at all, so it cannot price the scope,
/// style boundary and content slot a View carries.
/// </summary>
internal static class ViewScene
{
    /// <summary>Nodes per row: the view, its content stack, the label and the fixed editor leaf.</summary>
    public const int NodesPerRow = 4;

    public static int Nodes(int rows) => rows * NodesPerRow + 1;

    public static UiSession Build(int rows)
    {
        var root = new VStack();
        for (var index = 0; index < rows; index++) root.Add(new BenchView());
        return new UiSession(root, new HeadlessSurface(), new UiViewport(HeadlessSurface.Width, HeadlessSurface.Height),
            null, new UiDispatchOptions(), null, new UiControlFactories());
    }
}

/// <summary>One view authored the way an application view is: a layout declared in the constructor.</summary>
internal sealed class BenchView : View
{
    public BenchView() => Content(ui => ui.VStack(ui.Text(""), new SizedElement()));
}

/// <summary>Builds, first-syncs and tears down the whole view scene; the per-View node cost is inside it.</summary>
internal sealed class BuildViewScene(int rows) : IUiCase
{
    public void Setup() { }
    public void Run()
    {
        using var session = ViewScene.Build(rows);
        session.Update();
    }
    public void Cleanup() { }
}

/// <summary>One full layout pass over a prepared view scene: the walk through every View node and its
/// content slot, which is what a frame that only re-lays out pays.</summary>
internal sealed class RelayoutViewScene(int rows) : IUiCase
{
    private UiSession _session = null!;

    public void Setup()
    {
        _session = ViewScene.Build(rows);
        _session.Update();
    }

    public void Run()
    {
        _session.Root.Invalidate(Invalidation.Layout | Invalidation.Tree);
        _session.Update();
    }

    public void Cleanup() => _session.Dispose();
}

internal abstract class ChainCase(int depth) : IUiCase
{
    /// <summary>Property absent from local values, styles and the theme chain; only the default branch answers.</summary>
    protected static readonly UiProperty<float> ProbeProperty = new("Probe", 4.5f);

    protected VStack ChainRoot = null!;
    protected Element Leaf = null!;

    public void Setup()
    {
        (ChainRoot, Leaf) = Scene.Chain(depth);
        Leaf.Set(UiProperties.IsModified, true);
    }

    public abstract void Run();

    public void Cleanup() => ChainRoot.Dispose();
}

internal sealed class GetLocalValue(int depth) : ChainCase(depth)
{
    private bool _sink;
    public override void Run() => _sink ^= Leaf.Get(UiProperties.IsModified);
}

internal sealed class GetThemeValue(int depth) : ChainCase(depth)
{
    private float _sink;
    public override void Run() => _sink += Leaf.Get(UiProperties.FontSize);
}

internal sealed class GetUnknownDefault(int depth) : ChainCase(depth)
{
    private float _sink;
    public override void Run() => _sink += Leaf.Get(ProbeProperty);
}

internal sealed class SetSameValue(int depth) : ChainCase(depth)
{
    public override void Run() => Leaf.Set(UiProperties.IsModified, true);
}

internal sealed class SetChangedValue(int depth) : ChainCase(depth)
{
    private bool _alternate;
    public override void Run()
    {
        _alternate = !_alternate;
        Leaf.Set(UiProperties.IsModified, _alternate);
    }
}

internal sealed class InvalidateRender(int depth) : ChainCase(depth)
{
    public override void Run() => Leaf.Invalidate(Invalidation.Render);
}

/// <summary>
/// Creates and disposes detached nodes carrying local property values. The cases differ only in how many local values
/// a node holds: <c>StoreTwoProperties - StoreOneProperty</c> prices the storage the second value needs, and
/// <c>StoreThreeProperties - StoreOneProperty</c> the storage that appears once the inline slots are exhausted.
/// </summary>
internal abstract class StoreCase(int nodes) : IUiCase
{
    private float _sink;

    protected abstract void Fill(Element element);

    public void Setup() { }

    public void Run()
    {
        for (var index = 0; index < nodes; index++)
        {
            var element = new SizedElement();
            Fill(element);
            _sink += element.Width;
            element.Dispose();
        }
    }

    public void Cleanup() { }
}

internal sealed class StoreOneProperty(int nodes) : StoreCase(nodes)
{
    protected override void Fill(Element element) => element.Set(UiProperties.Width, 90f);
}

internal sealed class StoreTwoProperties(int nodes) : StoreCase(nodes)
{
    protected override void Fill(Element element)
    {
        element.Set(UiProperties.Width, 90f);
        element.Set(UiProperties.FontSize, 13f);
    }
}

internal sealed class StoreThreeProperties(int nodes) : StoreCase(nodes)
{
    protected override void Fill(Element element)
    {
        element.Set(UiProperties.Width, 90f);
        element.Set(UiProperties.FontSize, 13f);
        element.Set(UiProperties.IsModified, true);
    }
}

internal abstract class BoundListCase(int rows) : IUiCase
{
    protected ObservableCollection<RowModel> Source = null!;
    protected VStack List = null!;
    protected UiSession Session = null!;

    public void Setup()
    {
        Source = Scene.Models(rows);
        List = new VStack().BindItems(Source, Scene.BoundRow);
        Session = Scene.Session(List);
        Session.Update();
        if (List.Items.Count != rows) throw new InvalidOperationException("Initial collection synchronization is incomplete.");
    }

    public abstract void Run();

    public void Cleanup() => Session.Dispose();
}

internal sealed class UpdateStatic(int rows) : BoundListCase(rows)
{
    public override void Run() => Session.Update();
}

internal sealed class AppendRemoveRow(int rows) : BoundListCase(rows)
{
    private bool _appended;
    public override void Run()
    {
        if (_appended) Source.RemoveAt(Source.Count - 1);
        else Source.Add(new RowModel(rows));
        _appended = !_appended;
        Session.Update();
    }
}

internal sealed class MoveFirstRowToEnd(int rows) : BoundListCase(rows)
{
    public override void Run()
    {
        Source.Move(0, Source.Count - 1);
        Session.Update();
    }
}

internal sealed class NotifyOneRowName(int rows) : BoundListCase(rows)
{
    private bool _renamed;
    public override void Run()
    {
        _renamed = !_renamed;
        Source[0].Name = _renamed ? "renamed-a" : "renamed-b";
        Session.Update();
    }
}

internal sealed class RefreshAllRows(int rows) : BoundListCase(rows)
{
    public override void Run()
    {
        List.RefreshItems();
        Session.Update();
    }
}

internal sealed class ResetAllRows(int rows) : BoundListCase(rows)
{
    public override void Run()
    {
        Source.Clear();
        for (var index = 0; index < rows; index++) Source.Add(new RowModel(index));
        Session.Update();
    }
}

/// <summary>
/// Same inspector-shaped rows as <see cref="BoundListCase"/>, but bound by a stable key to a plain
/// <see cref="List{T}"/> source, which is what an application view model hands over. Every mutation therefore goes
/// through <c>RefreshItems</c>, so the pair isolates reconcile cost from notification cost.
/// </summary>
internal abstract class KeyedListCase(int rows) : IUiCase
{
    protected List<RowModel> Source = null!;
    protected VStack List = null!;
    protected UiSession Session = null!;

    public void Setup()
    {
        Source = Scene.PlainModels(rows);
        List = new VStack().BindItems(Source, Scene.BoundRow, static row => row.Id, Rebind);
        Session = Scene.Session(List);
        Session.Update();
        if (List.Items.Count != rows) throw new InvalidOperationException("Initial keyed synchronization is incomplete.");
    }

    /// <summary>Hands the surviving row the new model instance of the same key, the way an inspector row must.</summary>
    protected static void Rebind(Element row, RowModel model) => ((Text)((HStack)row).Items[0]).Value = model.Name;

    public abstract void Run();

    public void Cleanup() => Session.Dispose();
}

internal sealed class KeyedUpdateStatic(int rows) : KeyedListCase(rows)
{
    public override void Run() => Session.Update();
}

internal sealed class KeyedRefreshUnchanged(int rows) : KeyedListCase(rows)
{
    public override void Run()
    {
        List.RefreshItems();
        Session.Update();
    }
}

internal sealed class KeyedAppendRemoveRow(int rows) : KeyedListCase(rows)
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

internal sealed class KeyedMoveFirstRowToEnd(int rows) : KeyedListCase(rows)
{
    public override void Run()
    {
        var first = Source[0];
        Source.RemoveAt(0);
        Source.Add(first);
        List.RefreshItems();
        Session.Update();
    }
}

internal sealed class KeyedRebuildSameKeys(int rows) : KeyedListCase(rows)
{
    public override void Run()
    {
        Source.Clear();
        for (var index = 0; index < rows; index++) Source.Add(new RowModel(index));
        List.RefreshItems();
        Session.Update();
    }
}

internal sealed class KeyedReplaceRowInstance(int rows) : KeyedListCase(rows)
{
    private bool _renamed;
    public override void Run()
    {
        _renamed = !_renamed;
        Source[0] = new RowModel(0) { Name = _renamed ? "renamed-a" : "renamed-b" };
        List.RefreshItems();
        Session.Update();
    }
}

internal abstract class PropertyBindingCase(int count) : IUiCase
{
    protected const string TypedA = "typed-a";
    protected const string TypedB = "typed-b";

    protected RowModel[] Models = null!;
    protected string[] NamesA = null!;
    protected string[] NamesB = null!;
    protected VStack Screen = null!;
    protected UiSession Session = null!;
    protected TextBox Editor = null!;

    /// <summary>One label per model bound one-way plus one editor bound two-way to the first model.</summary>
    protected static VStack BoundScreen(RowModel[] models, out TextBox editor)
    {
        var root = new VStack();
        foreach (var model in models) root.Add(new Text().Bind(Text.ValueProperty, model, row => row.Name));
        editor = new TextBox().BindTwoWay(TextBox.ValueProperty, models[0], row => row.Name);
        root.Add(editor);
        return root;
    }

    public void Setup()
    {
        Models = Enumerable.Range(0, count).Select(static index => new RowModel(index)).ToArray();
        NamesA = Models.Select((_, index) => "sync-a" + index).ToArray();
        NamesB = Models.Select((_, index) => "sync-b" + index).ToArray();
        Screen = BoundScreen(Models, out Editor);
        Session = Scene.Session(Screen);
    }

    public abstract void Run();

    public void Cleanup() => Session.Dispose();
}

internal sealed class CreateBindings(int count) : PropertyBindingCase(count)
{
    private int _sink;
    public override void Run()
    {
        using var screen = BoundScreen(Models, out _);
        _sink += screen.Items.Count;
    }
}

internal sealed class AttachAndFirstSync(int count) : PropertyBindingCase(count)
{
    private bool _sink;
    public override void Run()
    {
        var screen = BoundScreen(Models, out _);
        using var session = Scene.Session(screen);
        session.Update();
        _sink ^= ((Text)screen.Items[0]).Value == Models[0].Name;
    }
}

internal sealed class NotifyOneAndUpdate(int count) : PropertyBindingCase(count)
{
    private bool _alternate;
    public override void Run()
    {
        _alternate = !_alternate;
        Models[0].Name = _alternate ? NamesA[0] : NamesB[0];
        Session.Update();
    }
}

internal sealed class NotifyAllAndUpdate(int count) : PropertyBindingCase(count)
{
    private bool _alternate;
    public override void Run()
    {
        _alternate = !_alternate;
        for (var index = 0; index < Models.Length; index++) Models[index].Name = _alternate ? NamesA[index] : NamesB[index];
        Session.Update();
    }
}

internal sealed class UiEditWritesSource(int count) : PropertyBindingCase(count)
{
    private bool _alternate;
    public override void Run()
    {
        _alternate = !_alternate;
        Editor.Value = _alternate ? TypedA : TypedB;
        Session.Update();
    }
}

/// <summary>
/// One structural window over the same inspector-shaped scene: N model notifications must produce exactly one
/// reconcile of the keyed rows, and the containers stay blocked only while that window is open.
/// </summary>
internal abstract class TreeSyncCase(int rows) : IUiCase
{
    [Flags] protected enum Work { None = 0, Rows = 1, Choices = 2, Removed = 4 }

    protected readonly int Rows = rows;
    protected VStack Sections = null!, Choices = null!, Removed = null!;
    protected UiSession Session = null!;
    private readonly RowModel _model = new(-1);
    private RowModel[] _entries = null!;
    private readonly List<RowModel> _presented = [];

    /// <summary>Reconciles performed so far; the queue owes exactly one per measured operation.</summary>
    internal int Passes { get; set; }
    internal bool InputBlocked => Sections.IsInputBlocked || Choices.IsInputBlocked || Removed.IsInputBlocked;
    internal int RowCount => Sections.Items.Count;

    /// <summary>A row without property bindings: the reconcile, not a binding, writes what the row shows.</summary>
    protected static Element SectionRow(RowModel model) => new HStack().WithChildren(new Text(model.Name), new SizedElement());
    protected static void RebindRow(Element row, RowModel model) => ((Text)((HStack)row).Items[0]).Value = model.Name;

    public virtual void Setup()
    {
        _entries = new RowModel[Rows];
        for (var index = 0; index < Rows; index++) _entries[index] = new RowModel(index);
        _presented.AddRange(_entries);
        var root = new VStack();
        root.Add(Sections = new VStack().BindItems(_presented, SectionRow, static model => model.Id, RebindRow));
        root.Add(Choices = new VStack());
        root.Add(Removed = new VStack());
        Session = Scene.Session(root);
        Session.Update();
        if (Sections.Items.Count != Rows) throw new InvalidOperationException("Initial keyed synchronization is incomplete.");
        _model.PropertyChanged += (_, _) => Request(Work.Rows);
    }

    public void Run()
    {
        Notify();
        Flush();
    }

    /// <summary>The model reports one change per presented row.</summary>
    internal void Notify()
    {
        for (var index = 0; index < Rows; index++) _model.NotifySilently();
    }

    /// <summary>Runs the queued structural pass.</summary>
    internal void Flush() => Session.Update();

    /// <summary>Rebuilds the mirror the keyed binding reads and reconciles the live rows.</summary>
    protected void Reconcile()
    {
        Passes++;
        _presented.Clear();
        _presented.AddRange(_entries);
        Sections.RefreshItems();
    }

    public void Cleanup() => Session.Dispose();

    protected abstract void Request(Work work);
}

/// <summary>The application pattern before UIA4.10: a mask field, one hand-written dispatch and manual blocking of
/// the three dynamic containers.</summary>
internal sealed class ManualMaskQueue(int rows) : TreeSyncCase(rows)
{
    private Work _mask;
    private bool _blocked;

    protected override void Request(Work work)
    {
        var alreadyQueued = _mask != Work.None;
        _mask |= work;
        if (alreadyQueued) return;
        Block(true);
        Sections.Dispatch(() =>
        {
            var pending = _mask;
            _mask = Work.None;
            if (pending != Work.None) Reconcile();
            Block(false);
        });
    }

    private void Block(bool blocked)
    {
        if (_blocked == blocked) return;
        _blocked = blocked;
        Sections.IsInputBlocked = blocked;
        Choices.IsInputBlocked = blocked;
        Removed.IsInputBlocked = blocked;
    }
}

/// <summary>The UIA4.10 path: the same window requested through <see cref="DeferredTreeSync{TRequest}"/>.</summary>
internal sealed class DeferredTreeSyncQueue(int rows) : TreeSyncCase(rows)
{
    private DeferredTreeSync<Work> _queue = null!;

    public override void Setup()
    {
        base.Setup();
        _queue = new DeferredTreeSync<Work>(Sections, Work.None, static (left, right) => left | right, _ => Reconcile())
            .BlocksInput(Sections, Choices, Removed);
    }

    protected override void Request(Work work)
    {
        // The screen blocks once when the window opens and folds further notifications into the request.
        if (!_queue.IsPending) _queue.BlockInput();
        _queue.Request(work);
    }
}
