namespace TrueMoon.Aether.Scenes.Authoring;


/// <summary>Local built-in v1 Transform: position, pitch/yaw/roll in radians and positive scale.</summary>
/// <param name="X">Local X coordinate.</param>
/// <param name="Y">Local Y coordinate.</param>
/// <param name="Z">Local Z coordinate.</param>
/// <param name="Pitch">Pitch in radians.</param>
/// <param name="Yaw">Yaw in radians.</param>
/// <param name="Roll">Roll in radians.</param>
/// <param name="ScaleX">Positive X scale.</param>
/// <param name="ScaleY">Positive Y scale.</param>
/// <param name="ScaleZ">Positive Z scale.</param>
public readonly record struct TransformValue(double X, double Y, double Z, double Pitch = 0, double Yaw = 0,
    double Roll = 0, double ScaleX = 1, double ScaleY = 1, double ScaleZ = 1)
{
    internal IEnumerable<(string Key, double Value)> Properties()
    {
        yield return ("x", X); yield return ("y", Y); yield return ("z", Z);
        yield return ("pitch", Pitch); yield return ("yaw", Yaw); yield return ("roll", Roll);
        yield return ("sx", ScaleX); yield return ("sy", ScaleY); yield return ("sz", ScaleZ);
    }

    internal bool IsValid() => Properties().All(p => float.IsFinite((float)p.Value)) && (float)ScaleX > 0 && (float)ScaleY > 0 && (float)ScaleZ > 0;
}
