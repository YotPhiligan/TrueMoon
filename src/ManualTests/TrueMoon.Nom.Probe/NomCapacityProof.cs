using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Probe;

internal static class NomCapacityProof
{
    private static void Check(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
    private static byte[] Key(int number,int length=4){var key=new byte[length];BinaryPrimitives.WriteInt32BigEndian(key,number);return key;}
    private static NomJournalBatch Batch(NomKvStore store,IReadOnlyList<NomJournalMutation> changes)=>new(store.Sequence+1,store.Generation,store.Generation,Guid.NewGuid(),changes);
    private static void Refuse(NomKvStore store,Action operation)
    {
        var sequence=store.Sequence;var root=ResourceCodec.Hash(File.ReadAllBytes(Path.Combine(store.Root,"nom.root")));var journal=Directory.EnumerateFiles(Path.Combine(store.Root,"journal"),"*.nom").Sum(p=>new FileInfo(p).Length);
        try{operation();throw new InvalidOperationException("Above-ceiling operation admitted");}catch(InvalidDataException){}
        Check(!store.IsFaulted && store.Sequence==sequence && ResourceCodec.Hash(File.ReadAllBytes(Path.Combine(store.Root,"nom.root")))==root &&
            Directory.EnumerateFiles(Path.Combine(store.Root,"journal"),"*.nom").Sum(p=>new FileInfo(p).Length)==journal,"capacity refusal before IO/state mutation");
    }
    internal static int Run(string report)
    {
        var passed=new List<string>();var results=new List<object>();string? failure=null;var library=Program.LibraryInfo();var watch=Stopwatch.StartNew();
        var scratch=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,"capacity-"+Path.GetFileNameWithoutExtension(report));Directory.CreateDirectory(scratch);
        NomKvOpenArguments Arguments(string name)=>new(Path.Combine(scratch,name),Guid.NewGuid(),Guid.NewGuid());
        try
        {
            {
                var args=Arguments("frames");using var factory=new NomKvStoreFactory();using var store=factory.Create(args);
                var mutations=Enumerable.Range(1,32).Select(n=>new NomJournalMutation([(byte)n],new byte[128*1024-(n==32 ? 416 : 0)])).ToArray();
                store.WriteBatch(Batch(store,mutations));Check(store.Get([1])!.Length==128*1024,"exact maximum value");
                var tooLarge=mutations.Take(31).Append(new NomJournalMutation([32],new byte[128*1024-415])).ToArray();Refuse(store,()=>store.WriteBatch(Batch(store,tooLarge)));
                store.WriteBatch(Batch(store,Enumerable.Range(100,8192).Select(n=>new NomJournalMutation(Key(n),[])).ToArray()));
                Refuse(store,()=>store.WriteBatch(Batch(store,Enumerable.Range(100,8193).Select(n=>new NomJournalMutation(Key(n),[])).ToArray())));
                store.WriteBatch(Batch(store,[new(Key(9000,1024),[])]));Refuse(store,()=>store.WriteBatch(Batch(store,[new(Key(9001,1025),[])])));
                Refuse(store,()=>store.WriteBatch(Batch(store,[new([99],new byte[128*1024+1])])));
                results.Add(new{MaximumValue=131072,MaximumKey=1024,MaximumPayload=4194304,MaximumMutations=8192,AdjacentRefusals=true});passed.Add("ExactKeyValuePayloadMutationCeilings");
            }
            GC.Collect();GC.WaitForPendingFinalizers();
            {
                var args=Arguments("keys");using var factory=new NomKvStoreFactory();var store=factory.Create(args);
                for(var start=0;start<2_000_000;start+=8192)store.WriteBatch(Batch(store,Enumerable.Range(start,Math.Min(8192,2_000_000-start)).Select(n=>new NomJournalMutation(Key(n),[])).ToArray()));
                Check(store.KeyCount==2_000_000 && store.IndexBytes==200_000_000,"exact default maximum key count");Refuse(store,()=>store.WriteBatch(Batch(store,[new(Key(2_000_000),[])])));
                var sequence=store.Sequence;store.Dispose();using var reopened=factory.Open(args);Check(reopened.KeyCount==2_000_000 && reopened.Sequence==sequence && reopened.Get(Key(1_999_999)) is {Length:0},"maximum key replay");
                results.Add(new{MaximumKeys=2_000_000,IndexBytes=reopened.IndexBytes,ReplaySequence=sequence});passed.Add("TwoMillionKeysAndAdjacentRefusalReopen");
            }
            GC.Collect();GC.WaitForPendingFinalizers();
            {
                var args=Arguments("index-checkpoint");using var factory=new NomKvStoreFactory();var store=factory.Create(args);
                for(var start=0;start<250_000;start+=1024)
                {
                    var batch=Batch(store,Enumerable.Range(start,Math.Min(1024,250_000-start)).Select(n=>new NomJournalMutation(Key(n,n<185456?978:977),new byte[n<160?87:88])).ToArray());
                    try{store.WriteBatch(batch);}catch(InvalidDataException error)when(!store.IsFaulted && error.Message.Contains("segment capacity",StringComparison.Ordinal)){store.Compact();store.WriteBatch(batch);}
                }
                Check(store.IndexBytes==256L*1024*1024,"exact index byte ceiling");
                Refuse(store,()=>store.WriteBatch(Batch(store,[new(Key(249999,977),null),new(Key(300000,978),[])])));
                var receipt=store.Compact();Check(receipt.CheckpointBytes==256L*1024*1024 && !receipt.CleanupPending,"exact maximum physical checkpoint");
                store.WriteBatch(Batch(store,[new(Key(0,978),new byte[88])]));Refuse(store,()=>store.Compact());
                store.WriteBatch(Batch(store,[new(Key(0,978),new byte[87])]));Check(store.Compact().CheckpointBytes==256L*1024*1024,"restored exact checkpoint admission");
                store.Dispose();using var reopened=factory.Open(args);Check(reopened.IndexBytes==256L*1024*1024 && reopened.KeyCount==250000 && reopened.Get(Key(0,978))!.Length==87,"maximum checkpoint verified replay");
                results.Add(new{MaximumIndexBytes=256L*1024*1024,MaximumCheckpointBytes=receipt.CheckpointBytes,Keys=250000,AdjacentByteRefusal=true});passed.Add("Exact256MiBIndexAndCheckpointWithAdjacentRefusal");
            }
            GC.Collect();GC.WaitForPendingFinalizers();
            {
                var args=Arguments("replay");using var factory=new NomKvStoreFactory();var store=factory.Create(args);
                for(var n=0;n<65536;n++)store.WriteBatch(Batch(store,[new([1],[])]));Refuse(store,()=>store.WriteBatch(Batch(store,[new([1],[1])])));
                store.Dispose();store=factory.Open(args);Check(store.Sequence==65536 && store.Get([1]) is {Length:0},"exact replay batch ceiling");store.Compact();store.WriteBatch(Batch(store,[new([1],[2])]));
                Check(store.Sequence==65537 && store.Get([1])![0]==2,"explicit maintenance restores append budget");store.Dispose();
                results.Add(new{MaximumReplayBatches=65536,NextBatchRefusedUntilCompaction=true});passed.Add("65536DurableBatchesReopenAndMaintenanceResume");
            }
        }
        catch(Exception error){failure=error.ToString();Console.Error.WriteLine(error);}
        watch.Stop();var process=Process.GetCurrentProcess();process.Refresh();File.WriteAllText(report,JsonSerializer.Serialize(new{Completed=failure==null,Library=library,Passed=passed,Results=results,Failure=failure,
            Milliseconds=watch.Elapsed.TotalMilliseconds,PeakWorkingSetBytes=process.PeakWorkingSet64,Scope="Actual default KV key/value/payload/mutation/index/checkpoint/replay ceilings and immediately adjacent refusal; Flush(true), no physical disk-full/power-loss claim"},new JsonSerializerOptions{WriteIndented=true}));return failure==null?0:1;
    }
}
