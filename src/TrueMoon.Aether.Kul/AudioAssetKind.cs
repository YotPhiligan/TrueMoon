namespace TrueMoon.Aether.Kul;

/// <summary>Preparation policy; neither policy opens files during a scene tick.</summary>
public enum AudioAssetKind
{
    /// <summary>Decode once and share clip PCM among prepared voices.</summary>
    Sound = 1,
    /// <summary>Prepare independent native streaming voices.</summary>
    Music = 2
}
