using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Probe;

internal static class NomBackupProof
{
    private static readonly string[] Cuts=["write-before","write-partial","write-after","flush-after","close-before","close-after","publish-before","publish-after"];
    private static void Check(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
    internal static async Task<int> Run(string report)
    {
        var passed=new List<string>();var started=0;var killed=0;var reaped=0;string? failure=null;var library=Program.LibraryInfo();
        var scratch=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,"backup-"+Path.GetFileNameWithoutExtension(report));Directory.CreateDirectory(scratch);
        try
        {
            foreach(var restore in new[]{false,true}) foreach(var cut in Cuts)
            {
                var parent=Path.Combine(scratch,(restore?"restore-":"export-")+cut);Directory.CreateDirectory(parent);
                var args=new OpenArguments(Path.Combine(parent,"source"),Guid.NewGuid(),Guid.NewGuid(),true);var destination=Path.Combine(parent,"destination");var backup=Path.Combine(parent,"backup");
                AssetResourceVersion version;AssetSnapshotManifest manifest;var pin=Guid.NewGuid();
                using(var factory=new CatalogFactory())
                {
                    await using var owner=factory.Create(args);var asset=new AssetRecord(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"backup",[1],["scene"]);var bytes=new byte[2*1024*1024+17];Array.Fill(bytes,(byte)73);
                    var ticket=await owner.ReserveResourcesAsync(0,[new(asset,bytes.Length,ResourceCodec.Hash(bytes),bytes.Length)]);using var input=new MemoryStream(bytes);
                    version=(await owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{asset.Id,input}})).Resources.Single();manifest=await owner.CreateSnapshotAsync([version]);await owner.PinSnapshotAsync(manifest.Id,pin);
                    if(restore)await owner.ExportBackupAsync(backup);
                }
                var source=restore?backup:args.Root;var before=Snapshot(source);await Kill(args,backup,destination,restore,cut);
                Check(Snapshot(source)==before,"process interruption never changes source backup/catalog");Check(Directory.Exists(destination)==(cut=="publish-after"),"destination appears only after verified directory publication");
                using var recovery=new CatalogFactory();CatalogOwner restored;
                if(restore)
                    restored=recovery.Create(Directory.Exists(destination) ? args with{Root=destination,Create=false} : args with{Root=destination,Create=false,Restore=new(backup,destination,args.ProjectId,args.StoreId)});
                else
                {
                    if(!Directory.Exists(destination)){await using var original=recovery.Create(args with{Create=false});await original.ExportBackupAsync(destination);}
                    restored=recovery.Create(args with{Root=Path.Combine(parent,"recovered"),Create=false,Restore=new(destination,Path.Combine(parent,"recovered"),args.ProjectId,args.StoreId)});
                }
                await using(restored)
                {
                    Check(restored.Header.Generation==1 && await restored.FindResourceAsync(version.AssetId)==version,"restored exact head/generation");
                    await using(var snapshot=await restored.OpenSnapshotAsync(manifest.Id))
                    {await using var read=await snapshot.OpenResourceAsync(version);Check(await ResourceCodec.HashStream(read,default)==version.Digest && read.Length==version.Length,"restored pinned stream hash/length");}
                    try{await restored.RemoveSnapshotAsync(manifest.Id);throw new InvalidOperationException("Durable pin lost");}catch(InvalidDataException){}
                    await restored.ReleaseSnapshotPinAsync(pin);await restored.RemoveSnapshotAsync(manifest.Id);
                }
                passed.Add((restore?"Restore:":"Export:")+cut);
            }
            Check(started==16 && killed==16 && reaped==16,"all backup children killed/reaped");
        }
        catch(Exception error){failure=error.ToString();Console.Error.WriteLine(error);}
        File.WriteAllText(report,JsonSerializer.Serialize(new{Completed=failure==null,Library=library,Passed=passed,StartedChildren=started,KilledChildren=killed,ReapedChildren=reaped,Failure=failure,
            Scope="Explicit verified backup/restore process cutpoints; no physical disk-full/power-loss claim"},new JsonSerializerOptions{WriteIndented=true}));return failure==null?0:1;

        async Task Kill(OpenArguments args,string backup,string destination,bool restore,string cut)
        {
            var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{Assembly.GetExecutingAssembly().Location,"backup-child",args.Root,args.ProjectId.ToString(),args.StoreId.ToString(),backup,destination,restore.ToString(),cut})info.ArgumentList.Add(arg);
            using var child=new Process{StartInfo=info};var launched=false;var exited=false;
            try
            {
                Check(child.Start(),"backup child start");launched=true;started++;var errors=child.StandardError.ReadToEndAsync();
                Check(await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15))=="cut:"+cut && !child.HasExited,"exact live backup cutpoint");
                if(!restore){try{using var unlocked=new FileStream(Path.Combine(args.Root,"owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);throw new InvalidOperationException("source root released");}catch(IOException){}}
                child.Kill(true);killed++;await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));exited=true;reaped++;Check((await errors).Length==0,"backup child stderr");
            }
            finally{if(launched&&!exited){if(!child.HasExited){child.Kill(true);killed++;}await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));reaped++;}}
        }
    }
    private static string Snapshot(string root)=>CatalogCodec.Key(Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p)!="owner.lock").Order(StringComparer.Ordinal)
        .SelectMany(p=>new[]{Path.GetRelativePath(root,p),ResourceCodec.Hash(File.ReadAllBytes(p))}).ToArray());
    internal static int Child(string[] args)
    {
        try
        {
            var restore=bool.Parse(args[6]);var files=new CrashFiles(args[7]);using var factory=new CatalogFactory(fileOperations:files);
            var open=new OpenArguments(args[1],Guid.Parse(args[2]),Guid.Parse(args[3]),false);
            if(restore){files.Armed=true;using var result=factory.Create(open with{Root=args[5],Restore=new(args[4],args[5],open.ProjectId,open.StoreId)});}
            else{using var owner=factory.Create(open);files.Armed=true;owner.ExportBackupAsync(args[5]).GetAwaiter().GetResult();}
            throw new InvalidOperationException("Backup cutpoint not reached");
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private sealed class CrashFiles(string cut):NomFileOperations
    {
        internal bool Armed;
        private void At(string point){if(Armed && point==cut){Console.WriteLine("cut:"+cut);Console.Out.Flush();using var never=new ManualResetEvent(false);never.WaitOne();}}
        internal override void Write(FileStream file,ReadOnlyMemory<byte> bytes)
        {
            if(Armed && file.Name.Contains(".nom-",StringComparison.Ordinal))
            {At("write-before");if(cut=="write-partial"){base.Write(file,bytes[..Math.Max(1,bytes.Length/2)]);At("write-partial");}base.Write(file,bytes);At("write-after");}
            else base.Write(file,bytes);
        }
        internal override void Flush(FileStream file){base.Flush(file);if(file.Name.Contains(".nom-",StringComparison.Ordinal))At("flush-after");}
        internal override void Close(FileStream file)
        {if(file.Name.Contains(".nom-",StringComparison.Ordinal)){At("close-before");base.Close(file);At("close-after");}else base.Close(file);}
        internal override void PublishBackupDirectory(string source,string destination){At("publish-before");base.PublishBackupDirectory(source,destination);At("publish-after");}
    }
}
