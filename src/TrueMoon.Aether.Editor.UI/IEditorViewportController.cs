using TrueMoon.Argentis.Input;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Aether.Editor.UI.Translation;
using TrueMoon.Aether.Runtime.Frames;
namespace TrueMoon.Aether.Editor.UI;
/// <summary>Parent-owned viewport controller capability. UI owns disposal; adapters own their borrowed services.</summary>
public interface IEditorViewportController : IDisposable
{
    Guid ViewId { get; }
    bool IsActive { get; }
    bool IsGesturing { get; }
    bool CanPresent { get; }
    bool NeedsFrame { get; }
    bool IsTranslating { get; }
    TranslationPosition? TranslationCandidate { get; }
    bool Activate();
    void Observe(UiSession ui);
    bool PreviewInput(UiInput input, IInputContext context);
    bool HandleInput(UiInput input, IInputContext context);
    void CancelInput();
    void TranslationPresentationChanged();
    /// <summary>Captures an immutable overlay only if the incoming ticket matches the admitted presentation.</summary>
    /// <param name="frame">Ticket about to be painted.</param><returns>Overlay for its admitted logical geometry, or null.</returns>
    TranslationOverlay? CaptureTranslationOverlay(ViewportFrame frame);
}
