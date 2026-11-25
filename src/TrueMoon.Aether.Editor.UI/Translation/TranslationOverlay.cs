using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Editor.UI.Translation;

/// <summary>Frozen translation overlay for one admitted viewport draw. Contains no controller or UI objects.</summary>
/// <param name="Geometry">Projected gizmo from the presented camera and image bounds.</param>
/// <param name="Clip">Effective viewport clip.</param><param name="Ghosts">Immutable selected geometry.</param>
/// <param name="Handle">Active constraint, or None outside a drag.</param><param name="Delta">Candidate world-space displacement.</param>
public readonly record struct TranslationOverlay(TranslationGeometry Geometry, ViewportImageBounds Clip,
    ImmutableArray<PreviewPickPrimitive> Ghosts, TranslationHandle Handle, Vector3 Delta);
