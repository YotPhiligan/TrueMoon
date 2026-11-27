using TrueMoon.Argentis;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;

namespace TrueMoon.Argentis.Benchmarks;

/// <summary>
/// Single-threaded behavior check of every measured scenario. It runs the same setup, operation and teardown the
/// benchmarks use, so session owner-thread affinity and binding semantics fail here instead of inside a measurement.
/// </summary>
internal static class WorkloadVerification
{
    private static void Check(bool condition, string caseName)
    {
        if (!condition) throw new InvalidOperationException("Verification failed: " + caseName);
    }

    internal static void Verify()
    {
        NodeLifecycle();
        ViewNode();
        PropertyAccess();
        PropertyStore();
        CollectionBinding();
        KeyedBinding();
        VirtualizedBinding();
        PropertyBinding();
        ConditionalSlot();
        TreeSync();
    }

    private static void NodeLifecycle()
    {
        const int rows = 39;
        var text = new FixedTextLayout();
        Check(Scene.Nodes(rows) == 196, "node count");
        var tree = Scene.Inspector(rows);
        Check(tree.Items.Count == rows, "scene rows");
        tree.Measure(Scene.Available, text);
        Check(tree.DesiredSize.Height > 0, "scene measure");
        tree.Arrange(Scene.Slot);
        var row = (HStack)tree.Items[0];
        Check(row.Bounds.Height > 0 && row.Items[1].Bounds.Height > 0, "row arrange");
        ((Text)row.Items[0]).Value = "edit-a";
        tree.Measure(Scene.Available, text);
        tree.Arrange(Scene.Slot);
        Check(row.Items[0].Bounds.Width > 0, "leaf edit layout");
        tree.Dispose();
        Check(tree.IsDisposed && row.IsDisposed, "scene dispose");
    }

    private static void ViewNode()
    {
        const int rows = 12;
        Check(ViewScene.Nodes(rows) == 49, "view node count");
        using var session = ViewScene.Build(rows);
        session.Update();
        var root = (VStack)session.Root;
        Check(root.Items.Count == rows, "view scene rows");
        var view = (BenchView)root.Items[0];
        Check(view.Children.Count == 1 && ReferenceEquals(view.Child, view.Children[0]), "view content slot");
        var body = ContentBody(view.Child);
        Check(body.Items.Count == 2 && ((Text)body.Items[0]).Value.Length == 0 && body.Items[1] is SizedElement,
            "view content children");
        view.Measure(Scene.Available, new FixedTextLayout());
        Check(view.DesiredSize.Height > 0, "view measure");
    }

    private static ElementList ContentBody(Element? content) =>
        content as ElementList ?? throw new InvalidOperationException("The view content is not a container.");

    private static void PropertyAccess()
    {
        var (root, leaf) = Scene.Chain(32);
        leaf.Set(UiProperties.IsModified, true);
        Check(leaf.Get(UiProperties.IsModified), "local get");
        Check(leaf.Get(UiProperties.FontSize) > 0, "theme get");
        root.Dispose();
        Check(root.IsDisposed, "chain dispose");
    }

    private static void PropertyStore()
    {
        var one = new SizedElement();
        one.Set(UiProperties.Width, 90f);
        Check(one.Get(UiProperties.Width) == 90f, "store one");
        var three = new SizedElement();
        three.Set(UiProperties.Width, 90f);
        three.Set(UiProperties.FontSize, 13f);
        three.Set(UiProperties.IsModified, true);
        Check(three.Get(UiProperties.Width) == 90f && three.Get(UiProperties.FontSize) == 13f &&
              three.Get(UiProperties.IsModified), "store three");
        three.Clear(UiProperties.Width);
        Check(float.IsNaN(three.Width) && three.Get(UiProperties.FontSize) == 13f, "store clear first");
        var two = new SizedElement();
        two.Set(UiProperties.Width, 90f);
        two.Set(UiProperties.FontSize, 13f);
        two.Clear(UiProperties.FontSize);
        Check(two.Get(UiProperties.Width) == 90f && two.FontSize == two.Theme.FontSize, "store two clear second");
        two.Dispose();
        Check(two.IsDisposed, "store two dispose");
        three.Dispose();
        one.Dispose();
        Check(one.IsDisposed && three.IsDisposed, "store dispose");
    }

    private static void CollectionBinding()
    {
        var source = Scene.Models(3);
        var list = new VStack().BindItems(source, Scene.BoundRow);
        using var session = Scene.Session(list);
        session.Update();
        Check(list.Items.Count == 3, "initial rows");
        Check(((Text)((HStack)list.Items[0]).Items[0]).Value == "row-0", "initial row text");
        Check(list.Items.All(item => item.Bounds.Height > 0), "row layout");
        var first = list.Items[0];
        source.Move(0, 2);
        session.Update();
        Check(list.Items.Count == 3 && ReferenceEquals(list.Items[^1], first), "move identity");
        source[0].Name = "renamed-a";
        session.Update();
        Check(((Text)((HStack)list.Items[0]).Items[0]).Value == "renamed-a", "row notification");
        list.RefreshItems();
        session.Update();
        Check(list.Items.Count == 3, "refresh rows");
        source.RemoveAt(2);
        session.Update();
        Check(list.Items.Count == 2, "removed row");
        source.Clear();
        session.Update();
        Check(list.Items.Count == 0, "cleared rows");
    }

    private static void KeyedBinding()
    {
        var source = Scene.PlainModels(3);
        var list = new VStack().BindItems(source, Scene.BoundRow, static row => row.Id, static (row, model) => ((Text)((HStack)row).Items[0]).Value = model.Name);
        using var session = Scene.Session(list);
        session.Update();
        Check(list.Items.Count == 3, "keyed initial rows");
        var first = list.Items[0];
        var last = list.Items[2];
        source.Clear();
        for (var index = 2; index >= 0; index--) source.Add(new RowModel(index));
        list.RefreshItems();
        session.Update();
        Check(list.Items.Count == 3 && ReferenceEquals(list.Items[0], last) && ReferenceEquals(list.Items[2], first), "keyed rebuild identity");
        Check(((Text)((HStack)list.Items[2]).Items[0]).Value == "row-0", "keyed rebuilt row text");
        source[0] = new RowModel(2) { Name = "renamed-a" };
        list.RefreshItems();
        session.Update();
        Check(ReferenceEquals(list.Items[0], last) && ((Text)((HStack)list.Items[0]).Items[0]).Value == "renamed-a", "keyed instance rebind");
        var middle = list.Items[1];
        source.RemoveAt(1);
        list.RefreshItems();
        session.Update();
        Check(list.Items.Count == 2 && ReferenceEquals(list.Items[0], last) && ReferenceEquals(list.Items[1], first) && middle.IsDisposed, "keyed removed row disposed");
        source.Clear();
        list.RefreshItems();
        session.Update();
        Check(list.Items.Count == 0 && first.IsDisposed && last.IsDisposed, "keyed cleared rows");
    }

    private static void PropertyBinding()
    {
        var models = new[] { new RowModel(0), new RowModel(1) };
        var root = new VStack();
        var label = new Text().Bind(Text.ValueProperty, models[0], model => model.Name);
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, models[1], model => model.Name);
        root.WithChildren(label, editor);
        using var session = Scene.Session(root);
        Check(label.Value == "row-0" && editor.Value == "row-1", "initial two-way sync");
        models[0].Name = "sync-a";
        session.Update();
        Check(label.Value == "sync-a", "one-way notification");
        editor.Value = "typed-a";
        session.Update();
        Check(models[1].Name == "typed-a", "two-way commit");
    }

    private static void VirtualizedBinding()
    {
        const int rows = 2496;
        var items = Scene.PlainModels(rows);
        var list = new VirtualizingStack().BindVirtualizedItems(
            items,
            Scene.BoundRow,
            static item => item.Id,
            (row, item) => ((Text)((HStack)row).Items[0]).Value = item.Name);
        var scroll = new ScrollViewer();
        scroll.SetContent(list);
        using var session = Scene.Session(scroll);
        session.Update();
        session.Update();
        Check(list.Items.Count is > 0 and < rows, "virtualized window is smaller than the source");
        Check(scroll.Extent > 10 * rows, "virtualized extent covers the whole list");
        Check(((Text)((HStack)list.Items[0]).Items[0]).Value == "row-0", "window starts at the first row");
        scroll.Offset = 400;
        session.Update();
        Check(((Text)((HStack)list.Items[0]).Items[0]).Value != "row-0", "scroll rebinds the window");
        var before = scroll.Extent;
        items.Add(new RowModel(rows));
        list.RefreshItems();
        session.Update();
        Check(scroll.Extent > before, "extent follows the source");

        var plainSource = Scene.PlainModels(312);
        var plain = new VStack().BindItems(plainSource, Scene.BoundRow, static item => item.Id);
        var plainScroll = new ScrollViewer();
        plainScroll.SetContent(plain);
        using var plainSession = Scene.Session(plainScroll);
        plainScroll.Offset = 400;
        plainSession.Update();
        Check(plain.Items.Count == 312, "non-virtualized list keeps every row realized");
        Check(plainScroll.Offset > 0, "non-virtualized scroll moves");
    }

    private static void ConditionalSlot()
    {
        var host = new ScrollViewer();
        var slot = new ConditionalContent<ConditionalState?>(new ConditionalState(0),
            state => state == null ? null : ConditionalBranch.Build(8, state.Token));
        host.SetContent(slot);
        using var session = Scene.Session(host);
        session.Update();
        var first = slot.Child;
        Check(first is VStack { Items.Count: 8 } && slot.HasBranch, "initial branch");
        slot.State = new ConditionalState(0);
        session.Update();
        Check(ReferenceEquals(slot.Child, first), "equal state keeps the branch");
        slot.State = new ConditionalState(1);
        session.Update();
        Check(slot.Child is VStack branch && !ReferenceEquals(branch, first), "new state rebuilds");
        Check(first!.IsDisposed, "replaced branch is released");
        slot.State = null;
        session.Update();
        Check(!slot.HasBranch && slot.Child == null, "null state releases the branch");

        // The Editor inspector hands over a freshly built token of keys and values on every model notification.
        var keys = new[] { "x", "y", "z" };
        string[] values = ["1", "2", "3"];
        var block = new ConditionalContent<PropertyBlockToken>(new PropertyBlockToken([.. keys], [.. values]), state =>
        {
            var lines = new VStack();
            for (var index = 0; index < state.Keys.Length; index++) lines.Add(new Text($"{state.Keys[index]}={state.Values[index]}"));
            return lines;
        });
        using var blockSession = Scene.Session(block);
        blockSession.Update();
        var firstBlock = block.Child;
        Check(firstBlock is VStack { Items.Count: 3 } && block.HasBranch, "property token branch");
        block.State = new PropertyBlockToken([.. keys], [.. values]);
        blockSession.Update();
        Check(ReferenceEquals(block.Child, firstBlock), "fresh equal token keeps the block");
        values[1] = "9";
        block.State = new PropertyBlockToken([.. keys], [.. values]);
        blockSession.Update();
        Check(block.Child is VStack changedBlock && !ReferenceEquals(changedBlock, firstBlock), "changed token rebuilds the block");
        Check(firstBlock!.IsDisposed, "replaced property block is released");
        session.Dispose();
        Check(host.IsDisposed && slot.IsDisposed, "conditional slot dispose");
    }

    private static void TreeSync()
    {
        const int rows = 39;
        var manual = new ManualMaskQueue(rows);
        manual.Setup();
        manual.Notify();
        Check(manual.InputBlocked, "manual window blocks input while it is open");
        Check(manual.RowCount == rows, "manual rows before the pass");
        manual.Flush();
        Check(manual.Passes == 1, "manual mask folds N notifications into one pass");
        Check(!manual.InputBlocked, "manual pass releases input blocking");
        Check(manual.RowCount == rows, "manual reconcile keeps the rows");
        manual.Run();
        Check(manual.Passes == 2, "manual window reopens for the next operation");
        manual.Cleanup();

        var deferred = new DeferredTreeSyncQueue(rows);
        deferred.Setup();
        deferred.Notify();
        Check(deferred.InputBlocked, "engine window blocks input while it is open");
        Check(deferred.RowCount == rows, "engine rows before the pass");
        deferred.Flush();
        Check(deferred.Passes == 1, "engine queue folds N notifications into one pass");
        Check(!deferred.InputBlocked, "engine pass releases input blocking");
        Check(deferred.RowCount == rows, "engine reconcile keeps the rows");
        deferred.Run();
        Check(deferred.Passes == 2, "engine window reopens for the next operation");
        deferred.Cleanup();
    }
}
