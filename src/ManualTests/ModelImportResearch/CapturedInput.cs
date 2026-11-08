#if NETTERLOW
using System.Security.Cryptography;
using System.Text;

namespace ModelImportResearch;

// Research profile, deliberately separate from production render-resource budgets.
internal sealed record ImportLimits(
    int Files = 32, int FileBytes = 16 * 1024 * 1024, int TotalBytes = 32 * 1024 * 1024,
    long ReadBytes = 128 * 1024 * 1024, int Opens = 512, int Streams = 16,
    int Meshes = 64, int Vertices = 65536, int Faces = 65536,
    int Nodes = 4096, int Depth = 64, int Instances = 4096, int OutputBytes = 8 * 1024 * 1024)
{
    internal void Validate()
    {
        if (Files is < 1 or > 32 || FileBytes is < 1 or > 16 * 1024 * 1024 ||
            TotalBytes is < 1 or > 32 * 1024 * 1024 || ReadBytes is < 1 or > 128 * 1024 * 1024 ||
            Opens is < 1 or > 512 || Streams is < 1 or > 16 || Meshes is < 1 or > 64 ||
            Vertices is < 1 or > 65536 || Faces is < 1 or > 65536 || Nodes is < 1 or > 4096 ||
            Depth is < 1 or > 64 || Instances is < 1 or > 4096 || OutputBytes is < 1 or > 8 * 1024 * 1024)
            throw new InvalidDataException("Invalid import budget.");
    }
}

internal sealed class CapturedInput
{
    internal const string Recipe = "captured-import/1;AssimpNetter=6.0.5;win-x64;single;Triangulate+ValidateDataStructure;position-only-research/1";
    private readonly SortedDictionary<string, byte[]> files = new(StringComparer.Ordinal);
    internal string Root { get; }
    internal string Revision { get; }
    internal IReadOnlyList<string> Names => files.Keys.ToArray();
    internal int TotalBytes => files.Values.Sum(b => b.Length);

    internal CapturedInput(string root, IEnumerable<KeyValuePair<string, byte[]>> entries, ImportLimits limits)
    {
        limits.Validate(); Root = Normalize(root);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in entries)
        {
            var name = Normalize(entry.Key);
            if (files.Count >= limits.Files || !aliases.Add(name) || entry.Value.Length > limits.FileBytes ||
                (total = checked(total + entry.Value.Length)) > limits.TotalBytes)
                throw new InvalidDataException("Captured input quota or path collision.");
            files.Add(name, entry.Value.ToArray()); // Caller cannot mutate captured bytes.
        }
        if (!files.ContainsKey(Root)) throw new InvalidDataException("Missing captured root.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(Recipe); Append(Root);
        foreach (var (name, bytes) in files) { Append(name); hash.AppendData(BitConverter.GetBytes(bytes.Length)); hash.AppendData(bytes); }
        Revision = Convert.ToHexString(hash.GetHashAndReset());
        void Append(string text) { var bytes = Encoding.UTF8.GetBytes(text); hash.AppendData(BitConverter.GetBytes(bytes.Length)); hash.AppendData(bytes); }
    }

    internal static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path) || Encoding.UTF8.GetByteCount(path) > 256 || path[0] is '/' or '\\' ||
            path.Any(c => char.IsControl(c) || ":%?#\"<>|*".Contains(c)))
            throw new InvalidDataException("Invalid captured path.");
        var parts = path.Replace('\\', '/').Split('/');
        if (parts.Any(p => p is ".." or "" || p.EndsWith(' ') || p.EndsWith('.' ) && p != "."))
            throw new InvalidDataException("Invalid captured path segment.");
        var normalized = string.Join('/', parts.Where(p => p != "."));
        if (normalized.Length == 0) throw new InvalidDataException("Empty captured path.");
        return normalized;
    }

    internal ReadOnlyMemory<byte> Get(string name) => files.TryGetValue(Normalize(name), out var bytes)
        ? bytes : throw new FileNotFoundException("Dependency is outside captured closure: " + name);

    internal static CapturedInput FromDirectory(string directory, string root, string[] dependencies, ImportLimits limits, CancellationToken token = default)
    {
        limits.Validate(); token.ThrowIfCancellationRequested();
        var names = new[] { root }.Concat(dependencies).ToArray();
        if (names.Length > limits.Files) throw new InvalidDataException("Captured file count.");
        // Trusted local research capture. Each file is held read-only while copied; this is not an atomic directory snapshot.
        var entries = new List<KeyValuePair<string, byte[]>>(); long total = 0;
        foreach (var name in names)
        {
            token.ThrowIfCancellationRequested();
            var normalized = Normalize(name);
            var path = Path.GetFullPath(Path.Combine(directory, normalized));
            for (var item = new FileInfo(path) as FileSystemInfo; item != null; item = item is FileInfo f ? f.Directory : ((DirectoryInfo)item).Parent)
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Reparse capture is unsupported.");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var length = stream.Length;
            if (length > limits.FileBytes || (total = checked(total + length)) > limits.TotalBytes)
                throw new InvalidDataException("Captured byte quota.");
            var bytes = new byte[checked((int)length)];
            for (var offset = 0; offset < bytes.Length; offset += 65536)
            { token.ThrowIfCancellationRequested(); stream.ReadExactly(bytes.AsSpan(offset, Math.Min(65536, bytes.Length - offset))); }
            if (stream.ReadByte() != -1) throw new InvalidDataException("Capture changed.");
            entries.Add(new(normalized, bytes));
        }
        token.ThrowIfCancellationRequested(); return new(root, entries, limits);
    }

    internal void Write(string path)
    {
        using var writer = new BinaryWriter(File.Create(path), Encoding.UTF8);
        writer.Write(1); writer.Write(Root); writer.Write(files.Count);
        foreach (var (name, bytes) in files) { writer.Write(name); writer.Write(bytes.Length); writer.Write(bytes); }
    }

    internal static CapturedInput Read(string path, ImportLimits limits)
    {
        limits.Validate();
        using var stream = File.OpenRead(path);
        if (stream.Length > limits.TotalBytes + 32768L) throw new InvalidDataException("Packet quota.");
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (reader.ReadInt32() != 1) throw new InvalidDataException("Packet version.");
        string Name()
        {
            var length = reader.Read7BitEncodedInt();
            if (length is < 1 or > 256) throw new InvalidDataException("Packet path quota.");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Normalize(new UTF8Encoding(false, true).GetString(bytes));
        }
        var root = Name(); var count = reader.ReadInt32();
        if (count < 1 || count > limits.Files) throw new InvalidDataException("Packet count.");
        var entries = new List<KeyValuePair<string, byte[]>>(); long total = 0;
        for (var i = 0; i < count; i++)
        {
            var name = Name(); var length = reader.ReadInt32();
            if (length < 0 || length > limits.FileBytes || (total = checked(total + length)) > limits.TotalBytes)
                throw new InvalidDataException("Packet bytes.");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            entries.Add(new(name, bytes));
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Packet trailing data.");
        return new(root, entries, limits);
    }
}
#endif
