using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorDocumentCommandsTests
{
    [Fact]
    public void ComponentMutation_RejectsReplacedDocumentWithSameIdentityAndRevisionThenUsesFreshReference()
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var captured = owner.Document.Read().Content!.Document;
        owner.Store.File = new(DocumentData.Scene(), new("replacement"));
        owner.Run(DocumentCommand.Open, "replacement.aetherscene");
        var replacement = owner.Document.Read().Content!;
        Assert.NotSame(captured, replacement.Document); Assert.Equal(captured.Id, replacement.Document.Id);
        Assert.Equal(captured.Revision, replacement.Document.Revision);

        Assert.Throws<InvalidOperationException>(() => owner.Commands.EditComponents(captured, Edit(7, false)));

        Assert.Same(replacement, owner.Document.Read().Content); Assert.False(owner.Document.Read().IsDirty);
        Assert.True(owner.Commands.EditComponents(replacement.Document, Edit(7, true)).Succeeded);
        var changed = owner.Document.Read().Content!;
        Assert.True(changed.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["flag"].Value.GetBoolean());
        Assert.Equal(1, changed.UndoCount); Assert.True(owner.Document.Read().IsDirty);
    }

    [Fact]
    public void ComponentMutation_RechecksLiveRuntimeAndCancelledLifetimeWithoutPublishing()
    {
        using var lifetime = new CancellationTokenSource();
        using var owner = new DocumentUiFixture(lifetime: lifetime.Token);
        owner.Run(DocumentCommand.Open, "room.aetherscene");
        var before = owner.Document.Read().Content!;
        owner.RuntimeIdle = false;
        Assert.Throws<InvalidOperationException>(() => owner.Commands.EditComponents(before.Document, Edit(7, true)));
        owner.RuntimeIdle = true; lifetime.Cancel();
        Assert.Throws<InvalidOperationException>(() => owner.Commands.EditComponents(before.Document, Edit(7, true)));
        Assert.Same(before, owner.Document.Read().Content); Assert.Equal(0, before.UndoCount);
        Assert.False(owner.Document.Read().IsDirty);
    }

    [Fact]
    public async Task ComponentMutation_IsFencedWhileSaveOwnsDocumentAndResumesAfterCompletion()
    {
        await using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var before = owner.Document.Read().Content!; var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        owner.Store.Write = async (_, _, _, _) => { entered.TrySetResult(); await release.Task; return new("saved"); };
        try
        {
            Assert.True(owner.Commands.Execute(DocumentCommand.Save));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Throws<InvalidOperationException>(() => owner.Commands.EditComponents(before.Document, Edit(7, true)));
            Assert.Same(before, owner.Document.Read().Content); Assert.Equal(0, before.UndoCount);
            release.SetResult(); await owner.Commands.Pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(owner.Commands.EditComponents(before.Document, Edit(7, true)).Succeeded);
            Assert.Equal(1, owner.Document.Read().Content!.UndoCount); Assert.True(owner.Document.Read().IsDirty);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public void ComponentEdits_SaveCloseAndReopenRetainTypesNullPresenceAndResetProvenanceWithoutTransientHistory()
    {
        var store = new JsonSceneDocumentStore(new SceneFileCodec(DocumentData.Factory()));
        using var owner = new DocumentUiFixture(sceneStore: store);
        DocumentUiFixture.Wait(owner.Document.NewAsync(DocumentData.Scene()));
        var initial = owner.Document.Read().Content!;
        var added = new ComponentId(DocumentData.Id(40));
        const string text = "edited \"A\"\nМир 🛰️";
        var result = owner.Commands.EditComponents(initial.Document, new(DocumentData.Operation(7),
        [
            new(DocumentData.Child, DocumentData.Custom, "integer", PropertyValue.From(long.MinValue)),
            new(DocumentData.Child, DocumentData.Custom, "flag", PropertyValue.From(true)),
            new(DocumentData.Child, DocumentData.Custom, "text", PropertyValue.From(text)),
            new(DocumentData.Child, DocumentData.Custom, "nullable", PropertyValue.Null),
            new(DocumentData.Child, DocumentData.Transform, "x", Reset: true),
            new(DocumentData.Child, added, "number", PropertyValue.From(12.5d))
        ],
        [
            new(DocumentData.Child, added, ComponentChangeKind.Add, new(added, DocumentData.CustomType, 2)),
            new(DocumentData.Child, DocumentData.Interaction, ComponentChangeKind.Remove)
        ]));
        Assert.True(result.Succeeded); Assert.Equal(1, owner.Document.Read().Content!.UndoCount);
        owner.Run(DocumentCommand.Save, "components.aetherscene");
        Assert.False(owner.Document.Read().IsDirty);
        owner.Run(DocumentCommand.Close); owner.Run(DocumentCommand.Open, "components.aetherscene");

        var state = owner.Document.Read().Content!; var entity = state.Resolved.Entities[DocumentData.Child];
        var custom = entity.Components[DocumentData.Custom];
        Assert.Equal(long.MinValue, custom.Properties["integer"].Value.GetInteger());
        Assert.True(custom.Properties["flag"].Value.GetBoolean());
        Assert.Equal(text, custom.Properties["text"].Value.GetString());
        Assert.Equal(PropertyValueKind.Null, custom.Properties["nullable"].Value.Kind);
        Assert.Equal(12.5d, entity.Components[added].Properties["number"].Value.GetNumber());
        Assert.Equal(2d, entity.Components[DocumentData.Transform].Properties["x"].Value.GetNumber());
        Assert.False(entity.Components.ContainsKey(DocumentData.Interaction));
        var patch = state.Document.Entities.Single(item => item.Id == DocumentData.Child).Changes;
        Assert.Contains(DocumentData.Interaction, patch.RemovedComponents);
        Assert.DoesNotContain(patch.PropertyOverrides, edit => edit.ComponentId == DocumentData.Transform && edit.PropertyId == "x");
        Assert.Equal(8, state.Document.Revision); Assert.Equal(0, state.UndoCount); Assert.Equal(0, state.RedoCount);
        Assert.False(owner.Document.Read().IsDirty);
        Assert.Equal(new[] { DocumentData.Base, DocumentData.Hidden, DocumentData.Restored }, state.Document.Definitions.Select(item => item.Id));
        Assert.Equal(new long[] { 19, 20, 21 }, state.Document.Definitions.Select(item => item.Revision));
        var inherited = state.Document.Definitions.Single(item => item.Id == DocumentData.Base).Changes.AddedComponents.Single(item => item.Id == DocumentData.Custom);
        Assert.Equal(long.MaxValue, inherited.Properties["integer"].GetInteger()); Assert.Equal(DocumentData.Text, inherited.Properties["text"].GetString());
    }

    private static ComponentEditBatch Edit(long revision, bool value) => new(DocumentData.Operation(revision),
        [new(DocumentData.Child, DocumentData.Custom, "flag", PropertyValue.From(value))]);
}
