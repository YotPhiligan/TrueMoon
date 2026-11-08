using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.AssetTool;

/// <summary>A frozen description of this tool's deterministic demo, portable between processes and restored roots.</summary>
public sealed record DemoReceipt(int Schema, Guid ProjectId, Guid StoreId, Guid SourceId, Guid ShaderId, Guid MeshId,
    AssetResourceVersion Shader, AssetResourceVersion OldMesh, AssetResourceVersion CurrentMesh,
    AssetWriteTicket ShaderTicket, AssetWriteTicket OldMeshTicket, AssetWriteTicket CurrentMeshTicket,
    string SnapshotId, long SnapshotGeneration, Guid PinId, long Generation)
{
    public static readonly Guid ShaderKind = Guid.Parse("7c016529-cadc-4a58-aae0-58d52e6d7f37");
    public static readonly Guid MeshKind = Guid.Parse("f4bba6d4-f837-4fda-9c8c-8209342ed2cc");
    public const int OldMeshLength = 2 * 1024 * 1024 + 17;
    public const int CurrentMeshLength = 64 * 1024 + 17;
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static byte[] ShaderBytes() => Encoding.UTF8.GetBytes("// Nom opaque shader demo\nvoid main() {}\n");
    public static byte[] MeshBytes(bool current)
    {
        var bytes = new byte[current ? CurrentMeshLength : OldMeshLength];
        for (var n = 0; n < bytes.Length; n++) bytes[n] = unchecked((byte)(n * (current ? 31 : 17) + (current ? 79 : 23)));
        return bytes;
    }
    public static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static bool IsDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static DemoReceipt Read(string path, Guid project, Guid store)
    {
        if (new FileInfo(path).Length is < 1 or > 32768) throw new InvalidDataException("Demo receipt byte budget.");
        var value = JsonSerializer.Deserialize<DemoReceipt>(File.ReadAllBytes(path), Json) ?? throw new InvalidDataException("Missing demo receipt.");
        value.Validate(project, store); return value;
    }
    public void Validate(Guid project, Guid store)
    {
        Require(Schema == 1 && ProjectId == project && StoreId == store && project != Guid.Empty && store != Guid.Empty &&
            SourceId != Guid.Empty && ShaderId != Guid.Empty && MeshId != Guid.Empty && ShaderId != MeshId && PinId != Guid.Empty &&
            Generation == 3 && SnapshotGeneration == 2 && IsDigest(SnapshotId), "Demo receipt schema/identity.");
        Version(Shader, ShaderId, ShaderKind, ShaderBytes()); Version(OldMesh, MeshId, MeshKind, MeshBytes(false)); Version(CurrentMesh, MeshId, MeshKind, MeshBytes(true));
        Require(OldMesh.Revision != CurrentMesh.Revision, "Demo versions must differ.");
        Ticket(ShaderTicket, 1); Ticket(OldMeshTicket, 2); Ticket(CurrentMeshTicket, 3);
    }
    private static void Version(AssetResourceVersion version, Guid asset, Guid kind, byte[] expected)
    {
        Require(version != null && version.AssetId == asset && version.KindId == kind && IsDigest(version.Revision) &&
            version.Length == expected.Length && version.Digest == Digest(expected), "Demo receipt exact resource identity/content.");
    }
    private void Ticket(AssetWriteTicket ticket, long sequence)
    { Require(ticket != null && ticket.StoreId == StoreId && ticket.Sequence == sequence && IsDigest(ticket.RequestDigest), "Demo receipt operation identity."); }
    internal static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
}
