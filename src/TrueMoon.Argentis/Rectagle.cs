namespace TrueMoon.Argentis;

public class Rectagle : Element
{
    public float Width
    {
        get => Get<float>(nameof(Width)); 
        set => Set(nameof(Width), value);
    }
    
    public float Height
    {
        get => Get<float>(nameof(Height)); 
        set => Set(nameof(Height), value);
    }
}