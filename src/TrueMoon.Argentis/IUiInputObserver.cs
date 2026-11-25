using TrueMoon.Argentis.Input;

namespace TrueMoon.Argentis;

/// <summary>Observes input before focus/capture routing, for menus, overlays and global control activation.</summary>
public interface IUiInputObserver
{
    /// <summary>Higher values run first, allowing a popup owner to handle keyboard dismissal before its host.</summary>
    int InputPriority => 0;
    /// <summary>Observes normalized input. True consumes it before underlying controls can act.</summary>
    /// <param name="input">Current input.</param><param name="context">Borrowed owner-thread services.</param><returns>Whether consumed.</returns>
    bool PreviewInput(UiInput input, IInputContext context);
}