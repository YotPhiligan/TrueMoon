namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Game camera projections the inspector can present.</summary>
public enum InspectorCameraProjection
{
    /// <summary>Projects using a vertical field of view.</summary>
    Perspective,

    /// <summary>Projects using a fixed vertical frame size.</summary>
    Orthographic
}

/// <summary>The camera projection presented by an inspector section.</summary>
/// <param name="Projection">Current projection, including a staged switch.</param>
/// <param name="CanSwitch">Whether the other projection is currently selectable.</param>
public readonly record struct InspectorCameraProjectionState(InspectorCameraProjection Projection, bool CanSwitch);
