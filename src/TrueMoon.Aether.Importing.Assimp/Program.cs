using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Assimp.Unmanaged;
using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Importing.Assimp;

internal static class Program
{
    internal const string NativeSha256 = "4EC73281A4FDDB735A0A59180EBFCEAB122EC8A8EF136814B2FCCCFB7B78E944";
    private static int Main(string[] args)
    {
        if (args.Length != 2 || !OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64) return 2;
        // No native activation before the parent attaches and queries the Job limits.
        Console.WriteLine("TMIM/1 READY"); Console.Out.Flush();
        if (Console.ReadLine() != "GO") return 3;
        var activated = false;
        try
        {
            var source = ModelImportProtocol.ReadInput(args[0]);
            var texture = GltfProfile.Validate(source);
            var tags = GltfIdentityTags.Create(source, texture != null);
            var native = Path.Combine(AppContext.BaseDirectory, "assimp.dll");
            if (!File.Exists(native)) native = Path.Combine(AppContext.BaseDirectory, "runtimes/win-x64/native/assimp.dll");
            using (var stream = File.OpenRead(native))
                ModelImportProtocol.Require(Convert.ToHexString(SHA256.HashData(stream)) == NativeSha256, "Pinned native Assimp hash mismatch.");
            if (!AssimpLibrary.Instance.LoadLibrary(native)) throw new InvalidDataException("Assimp activation failed.");
            activated = true; VerifyNative(native);
            ModelImportCandidate result;
            using (var io = new CapturedIo(tags.Input))
            {
                result = NativeProjection.Import(source, io, tags, texture);
                io.ThrowIfFailed();
                ModelImportProtocol.Require(io.LiveStreams == 0, "Assimp retained callback streams after scene release.");
            }
            var bytes = ModelImportProtocol.Encode(result);
            using (var output = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.None)) output.Write(bytes);
        }
        catch (Exception error)
        {
            // Bounded and value-free diagnostics; source paths/content and arbitrary native error strings are not echoed.
            Console.Error.WriteLine("Model import rejected: " + error.GetType().Name); return 1;
        }
        finally { if (activated) AssimpLibrary.Instance.FreeLibrary(); }
        Console.WriteLine("TMIM/1 DONE"); return 0;
    }
    private static unsafe void VerifyNative(string expectedPath)
    {
        using var process = Process.GetCurrentProcess();
        var modules = process.Modules.Cast<ProcessModule>().Where(module => Path.GetFileName(module.FileName).Equals("assimp.dll", StringComparison.OrdinalIgnoreCase)).ToArray();
        ModelImportProtocol.Require(modules.Length == 1 && Path.GetFullPath(modules[0].FileName).Equals(Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase), "Unexpected native module.");
        var library = NativeLibrary.Load(expectedPath);
        try
        {
            uint Call(string name) => ((delegate* unmanaged[Cdecl]<uint>)NativeLibrary.GetExport(library, name))();
            ModelImportProtocol.Require(Call("aiGetVersionMajor") == 6 && Call("aiGetVersionMinor") == 0 && Call("aiGetVersionPatch") == 6 &&
                (Call("aiGetCompileFlags") & 0x20) == 0, "Assimp version or single-precision ABI mismatch.");
        }
        finally { NativeLibrary.Free(library); }
    }
}
