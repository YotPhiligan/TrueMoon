using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Storage;

internal sealed partial class NomKvStore
{
    internal NomBackupEntry[] BackupFiles()
    {
        lock(gate)
        {
            EnsureReady(); NomBinary.Require(!pendingTail,"Backup cannot include an incomplete journal tail.");
            var result=new List<NomBackupEntry> { new("nom.root",NomRootCodec.RootBytes,NomBinary.ZeroDigest) };
            foreach(var file in segments.Values)
                result.Add(new(Path.GetRelativePath(Root,file.Name).Replace('\\','/'),file.Length,NomBinary.ZeroDigest));
            return result.OrderBy(e => e.Path,StringComparer.Ordinal).ToArray();
        }
    }
}
