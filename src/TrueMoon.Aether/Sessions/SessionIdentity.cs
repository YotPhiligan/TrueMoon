namespace TrueMoon.Aether.Sessions;

public sealed record SessionIdentity(Guid SessionId, ulong Generation, string HostContractId,
    string GameBuildId, string Nonce, int ControlVersion = 3, int FrameVersion = 1, int SnapshotVersion = 2)
{
    public void Validate()
    {
        if (SessionId == Guid.Empty || Generation == 0 || ControlVersion != 3 || FrameVersion != 1 || SnapshotVersion != 2 ||
            !IsHash(HostContractId) || !IsHash(GameBuildId) || !IsHash(Nonce))
            throw new InvalidDataException("Invalid engine session identity/version.");
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
