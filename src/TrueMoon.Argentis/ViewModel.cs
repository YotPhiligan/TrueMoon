namespace TrueMoon.Argentis;

/// <summary>Base for presentation state and actions, with property notifications inherited from <see cref="Bindable"/>.</summary>
/// <remarks>Concrete models define validation, synchronization and ownership independently of controls, styles and rendering.</remarks>
public abstract class ViewModel : Bindable
{
}
