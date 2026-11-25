using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using Xunit;
using Xunit.Abstractions;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorReorderTests(ITestOutputHelper output)
{
    [Fact]
    public void ChangingNeighbor_KeepsTransformEditorAttachedFocusedAndEditingItsDraft()
    {
        using var fixture = new InspectorFixture();
        using var model = new SectionProjection(fixture);
        model.Refresh(InspectorFixture.Child);
        var transform = model.Components.Single(c => c.Id == DocumentData.Transform);
        var neighbor = model.Components.Single(c => c.Id == DocumentData.Custom);
        InspectorComponent[] alone = [transform], first = [transform, neighbor], second = [neighbor, transform];
        model.Present(alone);
        var view = EditorInspectorView.Create(model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view, width: 336, height: 600);
        ui.Update();
        var address = (transform.Id, "x");
        var input = Assert.IsType<TextBox>(view.Properties[address].Input);
        ui.Focus(input);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new(InputKind.Text, Text: "42"));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        ui.Update();
        Assert.Equal("42", view.Properties[address].Property.Value);
        Assert.True(input.IsFocused);
        Assert.Equal((1, 1), (input.SelectionStart, input.SelectionLength));

        var attachmentChanges = 0;
        input.AttachmentChanged += () => attachmentChanges++;
        model.Present(second); ui.Update();
        var neighborSection = view.Components[neighbor.Id].Section;
        var originalRows = view.Properties.ToDictionary(p => p.Key, p => p.Value.Row);
        var diagnostics = ui.EnableDiagnostics();
        // The normal regression takes one round trip. The optional profile repeats the exact same
        // model notification on a real CPU surface; no timing or allocation threshold is an assertion.
        var profile = Environment.GetEnvironmentVariable("TRUEMOON_EDITOR_REORDER_PROFILE") == "1";
        var warmup = profile ? 8 : 0;
        var samples = profile ? 32 : 2;
        var update = new double[samples]; var layout = new double[samples]; var draw = new double[samples];
        var bytes = new long[samples];
        for (var i = -warmup; i < samples; i++)
        {
            var order = (i & 1) == 0 ? first : second;
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            model.Present(order);
            Assert.True(ui.Update());
            var allocatedInFrame = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (i >= 0)
            {
                var frame = diagnostics.Snapshot;
                update[i] = frame.Update.Last.TotalMicroseconds;
                layout[i] = (frame.Measure.Last + frame.Arrange.Last).TotalMicroseconds;
                draw[i] = frame.Render.Last.TotalMicroseconds;
                bytes[i] = allocatedInFrame;
            }
            Assert.Same(view.Components[order[0].Id].Section, view.ComponentList.Children[0]);
            Assert.Same(view.Components[order[1].Id].Section, view.ComponentList.Children[1]);
            Assert.Equal(originalRows.Count, view.Properties.Count);
            foreach (var (key, row) in originalRows) Assert.Same(row, view.Properties[key].Row);
        }
        if (profile)
        {
            static double P95(double[] values) { Array.Sort(values); return values[(int)Math.Ceiling(values.Length * .95) - 1]; }
            output.WriteLine($"Reorder: samples={samples}, live rows={originalRows.Count}, attachment changes={attachmentChanges}, bytes/frame={bytes.Average():F0}, p95 us update={P95(update):F1}, layout={P95(layout):F1}, draw={P95(draw):F1}");
        }

        model.Present([neighbor with { Title = "Updated neighbor" }, transform]); ui.Update();
        Assert.Equal("Updated neighbor", view.Components[neighbor.Id].Header.Value);
        Assert.Same(neighborSection, view.Components[neighbor.Id].Section);
        model.Present(alone); ui.Update();
        Assert.True(neighborSection.IsDisposed);
        Assert.Same(input, view.Properties[address].Input);
        Assert.Equal(0, attachmentChanges);
        var focused = Assert.Single(ui.CaptureSemantics().Nodes, n => n.IsFocused);
        Assert.Equal($"editor.inspector.transform.{transform.Id.Value:N}.x", focused.Id);
        Assert.Equal((1, 1), (input.SelectionStart, input.SelectionLength));
        Assert.True(ui.HandleInput(new(InputKind.Text, Text: "3")).Handled);
        Assert.Equal("43", view.Properties[address].Property.Value);
        Assert.True(model.HasDraft);
        Assert.Equal(0, fixture.Owner.Document.Read().Content!.UndoCount);
    }

    // Only the presentation order varies; editing, commands and field instances belong to the real model.
    private sealed class SectionProjection(InspectorFixture fixture)
        : EditorInspectorViewModel(fixture.Owner.Commands, fixture.Preview), IEditorInspectorViewModel
    {
        private IReadOnlyList<InspectorComponent> _visible = [];
        IReadOnlyList<InspectorComponent> IEditorInspectorViewModel.Components => _visible;
        internal void Present(IReadOnlyList<InspectorComponent> components)
        {
            _visible = components;
            OnPropertyChanged(nameof(Components));
        }
    }
}
