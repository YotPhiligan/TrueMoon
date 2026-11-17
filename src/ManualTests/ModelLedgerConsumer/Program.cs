using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelImportResearch;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.ModelLedgerConsumer;

internal static class Program
{
    private static readonly Guid SourceId=new("f65b93a1-e878-4d02-81ea-ddc323d1fc56");
    private static readonly Guid GridId=new("09a71374-5a4d-43d5-a73f-c8a9e0c222e7");
    private static async Task<int> Main(string[] args)
    {
        if(args.Length==5 && args[0] is "reader" or "publisher") return await Child(args);
        if(args.Length is not (5 or 6) || args[0] is not ("cobalt" or "microsoft")) return 2;
        var evidence=Path.GetFullPath(args[4]); if(Directory.Exists(evidence)) throw new IOException("Use fresh evidence."); Directory.CreateDirectory(evidence);
        var legacyArgs=args.Take(5).ToArray(); legacyArgs[4]=Path.Combine(evidence,"legacy");
        if(await ModelImportConsumer.Program.Main(legacyArgs)!=0) return 1;
        var report=new Report { Provider=args[0] };
        try
        {
            await using var app=Build(args[0]); await app.StartAsync();
            var captures=app.Services.Resolve<IFactory<ModelSourceCapture,ModelSourceCaptureArguments>>()!;
            var imports=app.Services.Resolve<IFactory<ModelImportOperation,ModelImportArguments>>()!;
            var ledgers=app.Services.Resolve<IFactory<ModelImportLedgerSession,ModelImportLedgerArguments>>()!;
            var host=app.Services.Resolve<ModelImportHost>()!;
            if(args.Length==6)
            {
                var originalHash=Hash(args[5]); var handoff=Path.Combine(evidence,"handoff","source.tmledger"); Directory.CreateDirectory(Path.GetDirectoryName(handoff)!); File.Copy(args[5],handoff);
                await using var accepted=ledgers.Create(new(SourceId,handoff))!; var snapshot=await accepted.OpenAsync() ?? throw new InvalidDataException("Missing handoff.");
                report.HandoffFingerprint=Digest(snapshot); var updated=await accepted.PublishAsync(snapshot.Candidate,snapshot.Generation);
                Check(SameIds(Ids(snapshot),updated) && updated.Generation==snapshot.Generation+1 && Hash(args[5])==originalHash,"fresh package consumes and republishes source sidecar without changing source",report);
            }
            var sources=Path.Combine(evidence,"sources"); Fixtures.Create(sources);
            var path=Path.Combine(evidence,"records","model.tmledger"); await using var ledger=ledgers.Create(new(SourceId,path))!;
            Check(await ledger.OpenAsync()==null && !Directory.Exists(Path.GetDirectoryName(path)),"readonly missing open",report);
            var worker=new ModelImportWorkerDeployment(args[1],args[3],Directory.EnumerateFiles(args[1],"*",SearchOption.AllDirectories).ToDictionary(file=>Path.GetRelativePath(args[1],file).Replace('\\','/'),Hash));
            async Task<ModelImportCandidate> Import(string name,Guid? sourceId=null)
            {
                await using var capture=captures.Create(new(sourceId??SourceId,sources,name))!; var source=await capture.CaptureAsync();
                await using var operation=imports.Create(new(source,worker,Path.Combine(evidence,"scratch")))!; var result=await operation.ImportAsync();
                Check(operation.WorkerReaped && host.ActiveOperationCount==0,"native reaped before publication",report); report.NativeWorkers++; return result;
            }
            var root=JsonNode.Parse(File.ReadAllText(Path.Combine(sources,"scene.gltf")))!.AsObject();
            var primitives=root["meshes"]![0]!["primitives"]!.AsArray();
            for(var i=0;i<primitives.Count;i++) primitives[i]!["extras"]=new JsonObject { ["truemoon"]=new JsonObject { ["subasset"]=i==0 ? "front" : "back" } };
            void Save(JsonObject value,string name="scene.gltf") => File.WriteAllText(Path.Combine(sources,name),value.ToJsonString());
            Save(root); var firstCandidate=await Import("scene.gltf");
            Check(firstCandidate.Semantics.Meshes.SequenceEqual(new[] {"mesh/shared/primitive/front","mesh/shared/primitive/back"}),"authored primitive locators",report);
            Check(firstCandidate.Semantics.Instances.All(value=>value!=null && value.StartsWith("instance/node/parent/",StringComparison.Ordinal)),"authored node path provenance",report);
            var first=await ledger.PublishAsync(firstCandidate,0); Check(first.Generation==1 && first.Bindings.Count==13,"complete first record",report);
            var ids=Ids(first); var firstFingerprint=Digest(first); report.FirstFingerprint=firstFingerprint;
            Check(first.Meshes.All(mesh=>mesh.Geometry.Slots.Single().SlotId==mesh.SlotId && mesh.SlotId!=RenderContentProfile.DefaultSlotId),"permanent slots projected",report);
            await Restart(args[0],args[3],path,SourceId,first,report);
            var repeated=await ledger.PublishAsync(await Import("scene.gltf"),1); Check(repeated.Candidate.Revision==firstCandidate.Revision && SameIds(ids,repeated),"repeat keeps IDs and canonical CPU bytes",report);
            var bufferPath=Path.Combine(sources,"scene.bin"); var buffer=File.ReadAllBytes(bufferPath); BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(12),2); File.WriteAllBytes(bufferPath,buffer);
            root["accessors"]![0]!["max"]![0]=2; Save(root);
            var changed=await ledger.PublishAsync(await Import("scene.gltf"),2); Check(SameIds(ids,changed) && changed.Candidate.Revision!=firstCandidate.Revision && changed.Meshes[0].Geometry.Bounds.Maximum.X==2,"content changes revision without changing IDs",report);
            report.Groups.Add("NativeLocatorsPermanentSlotsRestartAndContentRevision");

            Reverse(root); Save(root); var reordered=await ledger.PublishAsync(await Import("scene.gltf"),3);
            Check(SameIds(ids,reordered) && reordered.Meshes[0].AssetId==first.Meshes[1].AssetId,"array reorder preserves semantic identities",report);
            Check(reordered.Materials[reordered.Candidate.Meshes[0].MaterialIndex].Definition.BaseColorSrgb==System.Numerics.Vector3.UnitY,"reordered native material association",report);
            root["meshes"]![0]!["name"]="renamed"; Save(root); var renamedCandidate=await Import("scene.gltf");
            var mapping=reordered.Bindings.Where(value=>value.Kind is ModelSubassetKind.Mesh or ModelSubassetKind.Slot or ModelSubassetKind.BindingSet)
                .Select(value=>new ModelSubassetRename(value.Key,new(value.Kind,value.Key.Locator.Replace("mesh/shared/","mesh/renamed/",StringComparison.Ordinal)))).ToArray();
            var before=Hash(path); await Expect<InvalidOperationException>(()=>ledger.PublishAsync(renamedCandidate,1),report); Check(Hash(path)==before && ledger.Current==reordered,"stale exact file and snapshot",report);
            var renamed=await ledger.PublishAsync(renamedCandidate,4,new(renames:mapping)); Check(IdSet(renamed).SequenceEqual(IdSet(first)),"explicit mesh/slot/binding rename preserves IDs",report);
            await Restart(args[0],args[3],path,SourceId,renamed,report);
            var full=root.DeepClone().AsObject(); root["meshes"]![0]!["primitives"]!.AsArray().RemoveAt(0); Save(root);
            var deleted=await ledger.PublishAsync(await Import("scene.gltf"),5); var tombstones=deleted.Ledger.Entries.Where(entry=>!entry.Active).ToArray();
            Check(tombstones.Length==6 && deleted.Bindings.Count==7,"deleted primitive, unused material and occurrences become tombstones",report);
            Save(full); var restoredCandidate=await Import("scene.gltf"); before=Hash(path);
            await Expect<InvalidDataException>(()=>ledger.PublishAsync(restoredCandidate,6),report); Check(Hash(path)==before,"implicit restore preserves exact old record",report);
            var restored=await ledger.PublishAsync(restoredCandidate,6,new(renames:tombstones.Select(entry=>new ModelSubassetRename(entry.Key,entry.Key))));
            Check(IdSet(restored).SequenceEqual(IdSet(first)) && restored.Ledger.Entries.All(entry=>entry.Active),"explicit restore revives original IDs",report);
            report.Groups.Add("NativeReorderRenameDeleteAndExplicitRestore");

            var ambiguous=full.DeepClone().AsObject(); foreach(var primitive in ambiguous["meshes"]![0]!["primitives"]!.AsArray()) primitive!.AsObject().Remove("extras");
            Save(ambiguous,"ambiguous.gltf"); var ambiguousCandidate=await Import("ambiguous.gltf");
            Check(ambiguousCandidate.Semantics.Meshes.All(value=>value==null),"ambiguous primitives have no invented locators",report);
            before=Hash(path); await Expect<InvalidDataException>(()=>ledger.PublishAsync(ambiguousCandidate,7),report); Check(Hash(path)==before,"ambiguous publication leaves exact head",report);
            var labels=restored.Bindings.Select(value=>new ModelSubassetLabel(value.Kind,value.Index,value.Key.Locator));
            var labelled=await ledger.PublishAsync(ambiguousCandidate,7,new(labels:labels)); Check(SameIds(Ids(restored),labelled),"explicit labels disambiguate current selectors",report);
            var binary=full.DeepClone().AsObject(); binary["buffers"]![0]!.AsObject().Remove("uri");
            WriteGlb(Path.Combine(sources,"converted.glb"),binary,File.ReadAllBytes(bufferPath));
            var converted=await ledger.PublishAsync(await Import("converted.glb"),8); Check(SameIds(Ids(restored),converted),"source format/root rename preserves identities",report);
            report.HeadPath=path; report.HeadFingerprint=Digest(converted); report.CpuRevision=firstCandidate.Revision;
            report.Groups.Add("ExplicitDisambiguationAndGlbSourceMove");

            var duplicateMaterial=full.DeepClone().AsObject(); duplicateMaterial["materials"]!.AsArray().Add(duplicateMaterial["materials"]![0]!.DeepClone());
            var duplicateMesh=full.DeepClone().AsObject(); duplicateMesh["meshes"]!.AsArray().Add(duplicateMesh["meshes"]![0]!.DeepClone());
            var duplicateNode=full.DeepClone().AsObject(); var newNodeIndex=duplicateNode["nodes"]!.AsArray().Count;
            duplicateNode["nodes"]!.AsArray().Add(new JsonObject { ["name"]="parent" }); duplicateNode["scenes"]![0]!["nodes"]!.AsArray().Add(newNodeIndex);
            var multipleScenes=full.DeepClone().AsObject(); multipleScenes["scenes"]!.AsArray().Add(multipleScenes["scenes"]![0]!.DeepClone());
            foreach(var (name,value) in new[] {("unused-material.gltf",duplicateMaterial),("unused-mesh.gltf",duplicateMesh),("duplicate-node.gltf",duplicateNode),("unnamed-scenes.gltf",multipleScenes)})
            {
                Save(value,name); var unsafeHints=await Import(name); before=Hash(path);
                await Expect<InvalidDataException>(()=>ledger.PublishAsync(unsafeHints,9),report); Check(Hash(path)==before,"source namespace ambiguity never auto-rebinds persistent IDs",report);
            }
            var unicode=full.DeepClone().AsObject(); unicode["meshes"]![0]!["name"]="корпус"; Save(unicode,"модель.gltf");
            var unicodeCandidate=await Import("модель.gltf");
            Check(unicodeCandidate.Semantics.Meshes.All(value=>value!=null && value.StartsWith("mesh/"+Uri.EscapeDataString("корпус")+"/",StringComparison.Ordinal)),"Unicode authored names and captured filename",report);
            report.Groups.Add("GlobalUnusedAndHierarchyAmbiguityAndUnicode");

            var gridFile=File.ReadAllBytes(Path.Combine(sources,"grid.glb")); var jsonLength=(int)BinaryPrimitives.ReadUInt32LittleEndian(gridFile.AsSpan(12));
            var grid=JsonNode.Parse(gridFile.AsSpan(20,jsonLength))!.AsObject(); grid["meshes"]![0]!["name"]="grid"; grid["nodes"]![0]!["name"]="grid-node";
            WriteGlb(Path.Combine(sources,"grid-labelled.glb"),grid,gridFile.AsSpan(28+jsonLength).ToArray());
            var crashPath=Path.Combine(evidence,"crash","grid.tmledger"); await using var crashLedger=ledgers.Create(new(GridId,crashPath))!;
            var seed=await crashLedger.PublishAsync(await Import("grid-labelled.glb",GridId),0);
            await CrashPublisher(args[0],args[3],crashPath,crashLedger,seed,evidence,report);
            await app.StopAsync(); Check(host.ActiveOperationCount==0,"App drained",report);
            var legacy=JsonNode.Parse(File.ReadAllText(Path.Combine(evidence,"legacy","report.json")))!.AsObject();
            report.LegacyAssertions=legacy["Assertions"]!.GetValue<int>(); report.LegacyWorkers=legacy["ReapedWorkers"]!.GetValue<int>();
            report.WorkerFiles=worker.Sha256;
            report.LoadedFramework=AppDomain.CurrentDomain.GetAssemblies().Where(assembly=>!assembly.IsDynamic && assembly.GetName().Name!.StartsWith("TrueMoon",StringComparison.Ordinal))
                .ToDictionary(assembly=>assembly.GetName().Name!,assembly=>Hash(assembly.Location));
            report.Completed=true;
        }
        catch(Exception error) { report.Failure=error.ToString(); Console.Error.WriteLine(error); }
        finally { File.WriteAllText(Path.Combine(evidence,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true})); }
        return report.Completed ? 0 : 1;
    }
    private static IApp Build(string provider) => App.Builder(builder=> {if(provider=="microsoft") builder.UseDI();})
        .Setup(context=> {context.UseAetherModelSourceCapture();context.UseAetherModelImport();}).Build();
    private static async Task<int> Child(string[] args)
    {
        await using var app=Build(args[1]); await app.StartAsync();
        var factory=app.Services.Resolve<IFactory<ModelImportLedgerSession,ModelImportLedgerArguments>>()!;
        await using var ledger=factory.Create(new(Guid.Parse(args[3]),args[2]))!; var current=await ledger.OpenAsync() ?? throw new InvalidDataException("Missing child head.");
        if(args[0]=="reader") { Console.Write(Digest(current)); return 0; }
        File.WriteAllText(args[4],"ready");
        while(true) current=await ledger.PublishAsync(current.Candidate,current.Generation);
    }
    private static Process Start(string host,params string[] args)
    {
        var info=new ProcessStartInfo(host) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); foreach(var argument in args) info.ArgumentList.Add(argument);
        return Process.Start(info)!;
    }
    private static async Task Restart(string provider,string host,string path,Guid sourceId,PublishedModelImport expected,Report report)
    {
        var before=Hash(path); using var child=Start(host,"reader",provider,path,sourceId.ToString(),"unused");
        var stdout=child.StandardOutput.ReadToEndAsync(); var stderr=child.StandardError.ReadToEndAsync();
        try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Check(child.ExitCode==0 && await stdout==Digest(expected) && (await stderr).Length==0 && Hash(path)==before,"new process reads exact durable identities without writes",report); report.Readers++; }
        finally { await Reap(child); }
    }
    private static async Task CrashPublisher(string provider,string host,string path,ModelImportLedgerSession ledger,PublishedModelImport seed,string evidence,Report report)
    {
        var ready=Path.Combine(evidence,"publisher-ready"); using var child=Start(host,"publisher",provider,path,GridId.ToString(),ready);
        var stdout=child.StandardOutput.ReadToEndAsync(); var stderr=child.StandardError.ReadToEndAsync(); var timer=Stopwatch.StartNew(); var seen=false; var reads=0; long observed=seed.Generation;
        try
        {
            while(timer.Elapsed<TimeSpan.FromSeconds(10) && !child.HasExited)
            {
                if(File.Exists(ready))
                {
                    var snapshot=await ledger.OpenAsync() ?? throw new InvalidDataException("Torn/missing head during replacement.");
                    Check(snapshot.Generation>=observed && SameIds(Ids(seed),snapshot) && snapshot.Candidate.Revision==seed.Candidate.Revision,"concurrent reader sees complete monotonic head",report); observed=snapshot.Generation; reads++;
                    if(Directory.EnumerateFiles(Path.GetDirectoryName(path)!,Path.GetFileName(path)+".*.pending").Any()) {seen=true;break;}
                }
                await Task.Delay(1);
            }
            Check(seen && !child.HasExited,"owned staging file observed before publisher kill",report); child.Kill(entireProcessTree:true);
        }
        finally { await Reap(child); await stdout; await stderr; }
        var current=await ledger.OpenAsync() ?? throw new InvalidDataException("Missing post-crash head.");
        Check(current.Generation>=observed && SameIds(Ids(seed),current),"kill preserves complete old or new candidate+ledger",report);
        var next=await ledger.PublishAsync(current.Candidate,current.Generation); Check(next.Generation==current.Generation+1,"OS released crashed writer lease",report);
        report.Publishers++; report.ConcurrentReads+=reads;
        await Restart(provider,host,path,GridId,next,report); report.Groups.Add("ConcurrentReadsAbruptPublisherExitAndRestart");
    }
    private static async Task Reap(Process child)
    {
        if(!child.HasExited) child.Kill(entireProcessTree:true); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
    private static void Reverse(JsonObject root)
    {
        var materials=root["materials"]!.AsArray(); var materialCount=materials.Count; root["materials"]=new JsonArray(materials.Reverse().Select(value=>value!.DeepClone()).ToArray());
        var primitives=root["meshes"]![0]!["primitives"]!.AsArray(); root["meshes"]![0]!["primitives"]=new JsonArray(primitives.Reverse().Select(value=>value!.DeepClone()).ToArray());
        foreach(var primitive in root["meshes"]![0]!["primitives"]!.AsArray()) primitive!["material"]=materialCount-1-primitive["material"]!.GetValue<int>();
        var nodes=root["nodes"]!.AsArray(); var count=nodes.Count; root["nodes"]=new JsonArray(nodes.Reverse().Select(value=>value!.DeepClone()).ToArray());
        foreach(var node in root["nodes"]!.AsArray()) if(node!["children"] is JsonArray children) node["children"]=new JsonArray(children.Reverse().Select(value=>(JsonNode?)JsonValue.Create(count-1-value!.GetValue<int>())).ToArray());
        foreach(var scene in root["scenes"]!.AsArray()) scene!["nodes"]=new JsonArray(scene["nodes"]!.AsArray().Select(value=>(JsonNode?)JsonValue.Create(count-1-value!.GetValue<int>())).ToArray());
    }
    private static void WriteGlb(string path,JsonObject root,byte[] bin)
    {
        var json=Encoding.UTF8.GetBytes(root.ToJsonString()); var padded=(json.Length+3)&~3; var binLength=(bin.Length+3)&~3;
        using var output=File.Create(path); using var writer=new BinaryWriter(output);
        writer.Write(0x46546c67u);writer.Write(2u);writer.Write((uint)(28+padded+binLength));writer.Write((uint)padded);writer.Write(0x4e4f534au);writer.Write(json);
        for(var i=json.Length;i<padded;i++) writer.Write((byte)32); writer.Write((uint)binLength);writer.Write(0x004e4942u);writer.Write(bin);
        for(var i=bin.Length;i<binLength;i++) writer.Write((byte)0);
    }
    private static Dictionary<string,Guid> Ids(PublishedModelImport record) => record.Ledger.Entries.Where(entry=>entry.Active).ToDictionary(entry=>entry.Key.Kind+":"+entry.Key.Locator,entry=>entry.Id);
    private static bool SameIds(Dictionary<string,Guid> expected,PublishedModelImport actual) {var ids=Ids(actual);return expected.Count==ids.Count && expected.All(pair=>ids.GetValueOrDefault(pair.Key)==pair.Value);}
    private static Guid[] IdSet(PublishedModelImport record) => record.Ledger.Entries.Where(entry=>entry.Active).Select(entry=>entry.Id).Order().ToArray();
    private static string Digest(PublishedModelImport record) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record.Generation.ToString(CultureInfo.InvariantCulture)+"\n"+record.Candidate.Revision+"\n"+string.Join("\n",record.Ledger.Entries.Select(entry=>$"{entry.Key.Kind}:{entry.Key.Locator}:{entry.Id:N}:{entry.Active}")))));
    private static string Hash(string path) {using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    private static void Check(bool value,string message,Report report) {if(!value)throw new InvalidDataException(message);report.Assertions++;}
    private static async Task Expect<T>(Func<Task> action,Report report) where T:Exception {try{await action();}catch(T){report.Assertions++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private sealed class Report
    {
        public bool Completed{get;set;} public string? Failure{get;set;} public string Provider{get;set;}=""; public int Assertions{get;set;} public int LegacyAssertions{get;set;}
        public int NativeWorkers{get;set;} public int LegacyWorkers{get;set;} public int Readers{get;set;} public int Publishers{get;set;} public int ConcurrentReads{get;set;}
        public string? FirstFingerprint{get;set;} public string? CpuRevision{get;set;} public string? HeadPath{get;set;} public string? HeadFingerprint{get;set;} public string? HandoffFingerprint{get;set;}
        public List<string> Groups{get;}=[]; public IReadOnlyDictionary<string,string>? WorkerFiles{get;set;}
        public Dictionary<string,string>? LoadedFramework{get;set;}
    }
}
