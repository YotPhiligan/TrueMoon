namespace TrueMoon.Aether.Runtime.Hosting;

/// <summary>Process composition selected before App construction; independent of Playing and Game/EditPreview.</summary>
public enum AetherLaunchRole
{
    /// <summary>User-facing project/editor application.</summary>
    Editor,
    /// <summary>Standalone local game, window and input.</summary>
    Player,
    /// <summary>Internal owned child requiring a validated RuntimeBootstrap.</summary>
    RuntimeChild
}
