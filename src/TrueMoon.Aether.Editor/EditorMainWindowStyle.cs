using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Neutral standalone wrapper; the nested EditorView owns its own scoped appearance.</summary>
public sealed class EditorMainWindowStyle : IViewStyle<EditorMainWindow>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<EditorMainWindow> styles)
        => styles.Root().Padding(new Thickness(0)).Background(Color.Transparent);
}
