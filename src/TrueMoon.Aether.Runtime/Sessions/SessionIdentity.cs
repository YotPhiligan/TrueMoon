using TrueMoon.Aether.Checkpoints;

namespace TrueMoon.Aether.Runtime.Sessions;

public sealed record SessionIdentity(Guid SessionId, ulong Generation, string HostContractId,
    string GameBuildId, string Nonce, int ControlVersion = 4, int FrameVersion = 2, int SnapshotVersion = GameCheckpoint.CurrentVersion)
{
    public void Validate()
    {
        if (SessionId == Guid.Empty || Generation == 0 || ControlVersion != 4 || FrameVersion != 2 || SnapshotVersion != GameCheckpoint.CurrentVersion ||
            !IsHash(HostContractId) || !IsHash(GameBuildId) || !IsHash(Nonce))
            throw new InvalidDataException("Invalid engine session identity/version.");
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
