using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace ModelImportResearch;

internal static class Program
{
    internal static int Main(string[] args)
    {
        #if NETTERLOW
        if(args[0]=="worker")return ImportWorker.Execute(args);
        if(args[0]=="captured")return CapturedProof.Run(args[1],args[2]).GetAwaiter().GetResult();
        #endif
        if(args[0]=="fixtures"){Fixtures.Create(args[1]);return 0;}
        var directory=args[0];var output=args[1];var observations=new List<object>();
        var completed=false;string? failure=null;object? native=null;
        try
        {
            Candidate.Activate();native=NativeInfo();
            foreach(var name in new[]{"scene.glb","scene.gltf","scene.obj","triangle.fbx","grid.glb"})
            {
                if(!Candidate.Supports(name)){observations.Add(new{Name=name,Supported=false});continue;}
                var result=Candidate.Import(Path.Combine(directory,name));Projection.Verify(result,name);
                var hash=Projection.Fingerprint(result);
                for(var repeat=0;repeat<10;repeat++)Projection.Require(Projection.Fingerprint(Candidate.Import(Path.Combine(directory,name)))==hash,"repeat canonical fingerprint");
                observations.Add(new{Name=name,Supported=true,Parts=result.Parts.Length,Instances=result.Instances.Length,Nodes=result.Nodes,Fingerprint=hash});
            }
            foreach(var name in new[]{"bad.gltf","missing.gltf","truncated.glb"})
            {
                Exception? error=null;try{Candidate.Import(Path.Combine(directory,name));}catch(Exception ex){error=ex;}
                Projection.Require(error!=null,"invalid input must fail: "+name);
                observations.Add(new{Name=name,ExpectedFailure=true,Error=error!.GetType().Name});
            }
            var times=new List<double>();var allocations=new List<long>();
            for(var repeat=0;repeat<7;repeat++)
            {
                var start=GC.GetTotalAllocatedBytes(true);var timer=Stopwatch.StartNew();
                var model=Candidate.Import(Path.Combine(directory,"grid.glb"));_ = Projection.Fingerprint(model);
                timer.Stop();times.Add(timer.Elapsed.TotalMilliseconds);allocations.Add(GC.GetTotalAllocatedBytes(true)-start);
            }
            times.Sort();allocations.Sort();
            observations.Add(new{Workload="grid16384vertices96774indices-import-and-CPU-projection",WarmSamples=7,MedianMs=times[3],MinMs=times[0],MaxMs=times[^1],MedianManagedAllocatedBytes=allocations[3],PeakProcessWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64});
            Projection.Require(Candidate.LiveImports==0,"tracked import owners drained");completed=true;
        }
        catch(Exception ex){failure=ex.ToString();Console.Error.WriteLine(ex);}
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            try { Candidate.Shutdown(); }
            catch(Exception ex) { completed=false;failure=(failure??"")+" Cleanup: "+ex; }
            File.WriteAllText(output,JsonSerializer.Serialize(new{Completed=completed,Candidate=Candidate.Name,Failure=failure,Native=native,LiveImports=Candidate.LiveImports,Observations=observations},new JsonSerializerOptions{WriteIndented=true}));
        }
        return completed?0:1;
    }

    internal static unsafe object? NativeInfo()
    {
        // Existing binding performs activation. This only queries the same already-loaded module.
        using var process=Process.GetCurrentProcess();
        var modules=process.Modules.Cast<ProcessModule>().Where(m=>Path.GetFileName(m.FileName).Equals("assimp.dll",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(m.FileName).Equals("Assimp64.dll",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(modules.Length==0){Projection.Require(Candidate.ExpectedNativeVersion==null,"expected native activation");return null;}
        Projection.Require(modules.Length==1,"one native Assimp provider per process");
        var path=modules[0].FileName;var library=NativeLibrary.Load(path);
        try
        {
            uint Call(string name)=>((delegate* unmanaged[Cdecl]<uint>)NativeLibrary.GetExport(library,name))();
            var major=Call("aiGetVersionMajor");var minor=Call("aiGetVersionMinor");var patch=Call("aiGetVersionPatch");
            Projection.Require($"{major}.{minor}.{patch}"==Candidate.ExpectedNativeVersion,"pinned native version");
            var flags=Call("aiGetCompileFlags");Projection.Require((flags&0x20)==0,"single precision ABI");
            Projection.Require(Path.GetFullPath(path).StartsWith(Path.GetFullPath(AppContext.BaseDirectory),StringComparison.OrdinalIgnoreCase),"output-local native delivery");
            return new{Path=path,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),Version=$"{major}.{minor}.{patch}",CompileFlags=flags};
        }
        finally{NativeLibrary.Free(library);}
    }
}
