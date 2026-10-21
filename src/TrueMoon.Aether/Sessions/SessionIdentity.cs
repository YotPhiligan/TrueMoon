namespace TrueMoon.Aether.Sessions;

public sealed record SessionIdentity(Guid SessionId, ulong Generation, string HostContractId,
    string GameBuildId, string Nonce, int ControlVersion = 2, int FrameVersion = 1, int SnapshotVersion = 1)
{
    public void Validate()
    {
        if (SessionId == Guid.Empty || Generation == 0 || ControlVersion != 2 || FrameVersion != 1 || SnapshotVersion != 1 ||
            !IsHash(HostContractId) || !IsHash(GameBuildId) || !IsHash(Nonce))
            throw new InvalidDataException("Invalid engine session identity/version.");
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
