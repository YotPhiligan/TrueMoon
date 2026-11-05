using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorWindowCaptionTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Custom · Base")]
    public void Caption_FormatsNoProjectOpenDirtySavedAndClosedStateWithoutChangingDraftOrDoingIo(string baseTitle)
    {
        using var owner = new DocumentUiFixture(false); using var model = new EditorDocumentViewModel(owner.Commands);
        model.Refresh(); model.PathDraft = "retained path";
        Assert.Equal(baseTitle, model.GetWindowCaption(baseTitle)); Assert.Equal(baseTitle + "\nПроект не открыт", model.GetWindowCaptionDescription(baseTitle));
        DocumentUiFixture.Wait(owner.Projects.OpenAsync(owner.Files.Project.ManifestPath)); owner.Run(DocumentCommand.Open, "room.aetherscene"); model.Refresh();
        var path = Path.Combine(owner.Files.Project.ScenesDirectory, "room.aetherscene");
        Assert.Equal(baseTitle + " · room.aetherscene", model.GetWindowCaption(baseTitle));
        Assert.Equal(baseTitle + " · room.aetherscene\n" + path, model.GetWindowCaptionDescription(baseTitle));
        owner.Run(DocumentCommand.New); model.Refresh();
        Assert.Equal(baseTitle + " · Новая сцена *", model.GetWindowCaption(baseTitle));
        Assert.Equal(baseTitle + " · Новая сцена *\n" + owner.Files.Project.ScenesDirectory, model.GetWindowCaptionDescription(baseTitle));
        owner.Run(DocumentCommand.SaveAs, "saved.aetherscene"); model.Refresh();
        Assert.Equal(baseTitle + " · saved.aetherscene", model.GetWindowCaption(baseTitle));
        var reads = owner.Store.Reads.Count; var writes = owner.Store.Writes.Count; var changes = 0;
        model.PropertyChanged += (_, _) => changes++;
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(baseTitle + " · saved.aetherscene", model.GetWindowCaption(baseTitle));
            Assert.Equal(baseTitle + " · saved.aetherscene\n" + Path.Combine(owner.Files.Project.ScenesDirectory, "saved.aetherscene"), model.GetWindowCaptionDescription(baseTitle));
        }
        Assert.Equal(reads, owner.Store.Reads.Count); Assert.Equal(writes, owner.Store.Writes.Count); Assert.Equal(0, changes);
        Assert.Equal("retained path", model.PathDraft);
        owner.Run(DocumentCommand.Close); model.Refresh();
        Assert.Equal(baseTitle, model.GetWindowCaption(baseTitle));
        Assert.Equal(baseTitle + "\n" + owner.Files.Project.ScenesDirectory, model.GetWindowCaptionDescription(baseTitle));
    }

    [Fact]
    public void Caption_SharesBusyActivityAndDescriptionFromOneSnapshotAndNeverStartsAnotherSave()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands);
        owner.Run(DocumentCommand.Open, "room.aetherscene"); model.Refresh();
        var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        owner.Store.Write = async (_, _, _, _) => { entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("written"); };
        Assert.True(owner.Commands.Execute(DocumentCommand.Save)); DocumentUiFixture.Wait(entered.Task);
        try
        {
            model.Refresh(); var before = model.Document;
            Assert.Equal("Editor · room.aetherscene · Сохранение…", model.GetWindowCaption("Editor"));
            Assert.Equal("Editor · room.aetherscene · Сохранение…\n" + owner.Files.ScenePath, model.GetWindowCaptionDescription("Editor"));
            Assert.Same(before, model.Document); Assert.Single(owner.Store.Writes); Assert.True(model.IsBusy); Assert.False(owner.Commands.Pending.IsCompleted);
        }
        finally { release.TrySetResult(); }
        owner.Complete(); model.Refresh(); Assert.Equal("Editor · room.aetherscene", model.GetWindowCaption("Editor"));
        Assert.Single(owner.Store.Writes); Assert.False(model.IsBusy);
    }

    [Fact]
    public void Caption_RejectsNullBaseWithoutChangingPresentationOrStartingWork()
    {
        using var owner = new DocumentUiFixture(); using var model = new EditorDocumentViewModel(owner.Commands); model.Refresh();
        var title = model.Title; var document = model.Document;
        Assert.Throws<ArgumentNullException>(() => model.GetWindowCaption(null!));
        Assert.Throws<ArgumentNullException>(() => model.GetWindowCaptionDescription(null!));
        Assert.Equal(title, model.Title); Assert.Same(document, model.Document); Assert.Empty(owner.Store.Reads); Assert.Empty(owner.Store.Writes);
        Assert.False(owner.Commands.Read().Busy); Assert.False(owner.Commands.Read().Exiting);
    }
}
