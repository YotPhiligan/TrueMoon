using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Player;

/// <summary>Plain optional HUD data; no renderer/control/native references.</summary>
public sealed record PlayerViewModel(string Title);

/// <summary>Minimal typed Player view; canvas composition is independent from Editor/Tooling.</summary>
public sealed class PlayerView : View<PlayerViewModel>, IUiInputObserver
{
    private readonly PlayerInput _input;
    /// <summary>Gets the viewport from the prepared UI tree.</summary>
    /// <exception cref="InvalidOperationException">The viewport has not been prepared or is no longer part of the view.</exception>
    public PlayerCanvas Canvas => Child as PlayerCanvas
        ?? throw new InvalidOperationException("PlayerCanvas is available only after PlayerView content is prepared.");

    /// <summary>Declares the Player viewport; the UI session creates it from the registered factory.</summary>
    /// <param name="data">Borrowed Player view data.</param>
    /// <param name="input">Borrowed input state shared with the viewport and window.</param>
    public PlayerView(PlayerViewModel data, PlayerInput input)
    {
        _input = input;
        Content(data, static ui => ui.Create<PlayerCanvas>());
    }

    /// <inheritdoc />
    public bool PreviewInput(UiInput input,IInputContext context)
    {
        if(input.Kind==InputKind.FocusLost||input.Kind==InputKind.KeyDown&&input.Key==UiKey.Escape)
        {_input.Release();context.ReleaseCapture(Canvas);return input.Kind!=InputKind.FocusLost;}
        return false;
    }
}

/// <summary>Default viewport and HUD text style, separately replaceable from view composition.</summary>
public sealed class PlayerViewStyle:IViewStyle<PlayerView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<PlayerView> styles)
    {styles.Root().Padding(0).Background(theme=>theme.Surface);styles.Part(view=>view.Canvas).Padding(0).Set(UiProperties.CornerRadius,6f);}
}
