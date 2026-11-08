using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Probe;

internal static class NomRetentionProof
{
    private static readonly string[] Cuts = ["prune-write-before","prune-write-partial","prune-write-after","prune-flush-before","prune-flush-after",
        "delete-before","delete-after","ack-write-before","ack-write-partial","ack-write-after","ack-flush-before","ack-flush-after"];
    private static void Check(bool value,string reason) { if (!value) throw new InvalidDataException(reason); }
    private static readonly Guid Source = Guid.Parse("5a2b47df-a16f-4560-9931-09d5e24b908f");
    private static readonly Guid Kind = Guid.Parse("6382887b-e78c-4e2e-992f-5b7141fc64fc");
    private static readonly Guid Pin = Guid.Parse("28219896-49d2-4f60-a659-d705a1fb56bf");
    private static AssetRecord Asset(int number,AssetResourceVersion[]? dependencies=null) =>
        new(Guid.Parse($"{number:x8}-5687-4a4e-a5ed-35c6a1a85c40"),Source,Kind,"item/"+number,[],["geometry","scene"],dependencies);
    private static async Task<AssetResourceVersion> Publish(CatalogOwner owner,AssetRecord asset,byte[] bytes)
    {
        var ticket = await owner.ReserveResourcesAsync(owner.Header.Generation,[new(asset,bytes.Length,ResourceCodec.Hash(bytes),bytes.Length)]);
        using var input = new MemoryStream(bytes); return (await owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream> { [asset.Id]=input })).Resources.Single();
    }
    internal static async Task<int> Run(string report)
    {
        var passed = new List<string>(); var started=0; var killed=0; var reaped=0; string? failure=null;
        var library = Program.LibraryInfo(); var scratch = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,"retention-"+Path.GetFileNameWithoutExtension(report)); Directory.CreateDirectory(scratch);
        try
        {
            foreach (var cut in Cuts)
            {
                var arguments = new OpenArguments(Path.Combine(scratch,cut),Guid.NewGuid(),Guid.NewGuid(),true);
                var seed = await Seed(arguments); await Kill(arguments,cut);
                using var factory = new CatalogFactory(); await using var owner = factory.Create(arguments with { Create=false });
                Check(owner.Header.Generation==5,"GC keeps logical generation at every cut");
                var page = await owner.QueryAssetsAsync(new(Source,Kind,"scene")); Check(page.Items.Length==3 && page.Next==null,"source/kind/tag index rebuilt after interrupted GC");
                await using (var snapshot = await owner.OpenSnapshotAsync(seed.Manifest.Id))
                {
                    await using var old = await snapshot.OpenResourceAsync(seed.Old); using var output = new MemoryStream(); await old.CopyToAsync(output); Check(output.ToArray().AsSpan().SequenceEqual(new byte[] { 10 }),"pinned old bytes survive every GC cut");
                    Check(snapshot.Manifest.Members.Contains(seed.Dependency),"exact dependency closure survives replay");
                }
                await owner.CollectGarbageAsync(); var retry = await owner.PlanGarbageCollectionAsync(); await owner.CollectGarbageAsync(retry);
                var done = await owner.PlanGarbageCollectionAsync(); Check(done.RevisionsToPrune==0 && done.ObjectsToDelete==0,"interrupted prune/delete resumes to empty garbage set");
                var dead = Path.Combine(arguments.Root,"objects","sha256",seed.Dead.Digest[..2],seed.Dead.Digest+".blob"); Check(!File.Exists(dead),"only unreachable CAS removed");
                try { await owner.RemoveSnapshotAsync(seed.Manifest.Id); throw new InvalidOperationException("Durable pin lost"); } catch(InvalidDataException) { }
                await owner.ReleaseSnapshotPinAsync(Pin); await owner.RemoveSnapshotAsync(seed.Manifest.Id); var final = await owner.PlanGarbageCollectionAsync();
                Check(final.RevisionsToPrune==1 && final.ObjectsToDelete==1,"only unpinned old parent becomes garbage; dependency head remains live"); await owner.CollectGarbageAsync(final);
                Check(await owner.FindResourceAsync(seed.Current.AssetId)==seed.Current,"current head remains exact"); passed.Add("Recovered:"+cut);
            }
            Check(started==Cuts.Length && killed==started && reaped==started,"all retention children killed/reaped");
        }
        catch(Exception error) { failure=error.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(report,JsonSerializer.Serialize(new { Completed=failure==null,Library=library,Passed=passed,StartedChildren=started,KilledChildren=killed,ReapedChildren=reaped,Failure=failure,
            Scope="NOM5 exact indexes/manifests/pins plus interrupted GC prune/delete/ack; process kill is not physical-volume or power-loss proof" },new JsonSerializerOptions { WriteIndented=true }));
        return failure==null ? 0 : 1;

        async Task Kill(OpenArguments arguments,string cut)
        {
            var info = new ProcessStartInfo("dotnet") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
            foreach (var arg in new[] { Assembly.GetExecutingAssembly().Location,"retention-child",arguments.Root,arguments.ProjectId.ToString(),arguments.StoreId.ToString(),cut }) info.ArgumentList.Add(arg);
            using var child = new Process { StartInfo=info }; var launched=false; var exited=false;
            try
            {
                Check(child.Start(),"retention child starts"); launched=true; started++; var errors=child.StandardError.ReadToEndAsync();
                var line=await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)); Check(line=="cut:"+cut && !child.HasExited,"exact live retention cutpoint");
                try { using var unlocked = new FileStream(Path.Combine(arguments.Root,"owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None); throw new InvalidOperationException("Root released at cut"); } catch(IOException) { }
                child.Kill(true); killed++; await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); exited=true; reaped++; Check((await errors).Length==0,"retention child stderr");
            }
            finally { if (launched && !exited) { if (!child.HasExited) { child.Kill(true); killed++; } await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); reaped++; } }
        }
    }
    private static async Task<(AssetSnapshotManifest Manifest,AssetResourceVersion Old,AssetResourceVersion Dependency,AssetResourceVersion Dead,AssetResourceVersion Current)> Seed(OpenArguments arguments)
    {
        using var factory = new CatalogFactory(); await using var owner=factory.Create(arguments);
        var dependency=await Publish(owner,Asset(1),[1]); var old=await Publish(owner,Asset(2,[dependency]),[10]);
        var manifest=await owner.CreateSnapshotAsync([old]); await owner.PinSnapshotAsync(manifest.Id,Pin);
        var current=await Publish(owner,Asset(2),[20]); var dead=await Publish(owner,Asset(3),[30]); _=await Publish(owner,Asset(3),[40]);
        await owner.PruneResourceOperationsAsync(5); return (manifest,old,dependency,dead,current);
    }
    internal static int Child(string[] args)
    {
        try
        {
            var files=new CrashFiles(args[4]); using var factory=new CatalogFactory(fileOperations:files);
            using var owner=factory.Create(new(args[1],Guid.Parse(args[2]),Guid.Parse(args[3]),false)); var plan=owner.PlanGarbageCollectionAsync().GetAwaiter().GetResult();
            files.Armed=true; owner.CollectGarbageAsync(plan).GetAwaiter().GetResult(); throw new InvalidOperationException("Retention cut not reached");
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private sealed class CrashFiles(string cut) : NomFileOperations
    {
        internal bool Armed; private int phase;
        private string Phase => phase==0 ? "prune" : "ack";
        private void At(string point)
        { if (Armed && cut==point) { Console.WriteLine("cut:"+cut); Console.Out.Flush(); using var never=new ManualResetEvent(false); never.WaitOne(); } }
        internal override void Write(FileStream file,ReadOnlyMemory<byte> bytes)
        {
            if (Armed && cut==Phase+"-write-partial") { base.Write(file,bytes[..(bytes.Length/2)]); At(Phase+"-write-partial"); }
            At(Phase+"-write-before"); base.Write(file,bytes); At(Phase+"-write-after");
        }
        internal override void Flush(FileStream file)
        { At(Phase+"-flush-before"); base.Flush(file); At(Phase+"-flush-after"); if (Armed) phase++; }
        internal override void DeleteFile(string path)
        { At("delete-before"); base.DeleteFile(path); At("delete-after"); }
    }
}
