using System.Collections.Immutable;

namespace TrueMoon.Aether.Editor.Translation;

/// <summary>A world-space translation constraint. Local-oriented handles are outside v1.</summary>
public enum TranslationHandle
{
    /// <summary>No admitted handle.</summary>
    None,
    /// <summary>World X axis.</summary>
    X,
    /// <summary>World Y axis.</summary>
    Y,
    /// <summary>World Z axis.</summary>
    Z,
    /// <summary>World XY plane.</summary>
    XY,
    /// <summary>World XZ plane.</summary>
    XZ,
    /// <summary>World YZ plane.</summary>
    YZ
}

/// <summary>A finite logical screen coordinate, independent of controls and framebuffer scale.</summary>
/// <param name="X">Logical horizontal coordinate.</param><param name="Y">Logical vertical coordinate.</param>
public readonly record struct TranslationPoint(double X, double Y);

/// <summary>A projected axis segment or four-corner plane region.</summary>
/// <param name="Kind">World constraint.</param><param name="Points">Two axis endpoints or four plane corners.</param>
public sealed record TranslationHandleGeometry(TranslationHandle Kind, ImmutableArray<TranslationPoint> Points);

/// <summary>Canonical local position; unchanged coordinates retain their original double precision.</summary>
/// <param name="X">Local X.</param><param name="Y">Local Y.</param><param name="Z">Local Z.</param>
public readonly record struct TranslationPosition(double X, double Y, double Z);
