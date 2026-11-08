using Xunit;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class Nom6LimitsAndPagesTests
{
    [Fact]
    public void DomainIndexBudget_OptionCopiesConfiguredBytesAndRejectsOutsideSupportedRange()
    {
        var options=new NomOptions(); Assert.Equal(64L*1024*1024,options.DomainIndexBytes); options.DomainIndexBytes=256L*1024*1024;
        var snapshot=options.Snapshot(); options.DomainIndexBytes=1; Assert.Equal(256L*1024*1024,snapshot.Limits.DomainIndexBytes);
        Assert.Equal(1,options.Snapshot().Limits.DomainIndexBytes);
        foreach(var invalid in new[]{0L,-1,256L*1024*1024+1}) {options.DomainIndexBytes=invalid;Assert.Throws<ArgumentOutOfRangeException>(()=>options.Snapshot());}
    }

    [Theory]
    [InlineData(639,false)][InlineData(640,true)]
    public async Task DomainIndexBudget_PublicationAcceptsExactAssetRevisionCostAndRefusesPreviousByteAtomically(long budget,bool allowed)
    {
        await using var context=new Nom6Context(domainIndexBytes:budget); var asset=Nom5Context.Asset(1);
        if(allowed)
        {
            var published=await context.Publish(asset,[1]); Assert.Equal(1,context.Store.ContentGeneration); Assert.Equal(published.Version,await context.Store.FindResourceAsync(asset.Id));
            await context.Reopen(); Assert.Equal(published.Version,await context.Store.FindResourceAsync(asset.Id));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(()=>context.Publish(asset,[1])); Assert.Equal(0,context.Store.ContentGeneration); Assert.Null(await context.Store.FindResourceAsync(asset.Id));
            Assert.Empty((await context.Store.QueryAssetsAsync(new())).Items); await context.Reopen(); Assert.Equal(0,context.Store.ContentGeneration); Assert.Empty(await context.Store.ReadAsync());
        }
    }

    [Fact]
    public void KvReadPage_OrderedCacheMatchesPrefixContinuationAndByteModelBeforeAndAfterMutation()
    {
        using var context=new NomCheckpointContext(); using var store=context.Factory.Create(context.Directory.Arguments());
        var model=new SortedDictionary<byte[],byte[]>(NomKeyComparer.Instance);
        foreach(var prefix in new byte[]{1,2,3,255})for(var n=0;n<20;n++)model.Add([prefix,(byte)n],[(byte)n]);
        store.WriteBatch(new(1,0,1,Guid.NewGuid(),model.Reverse().Select(p=>new NomJournalMutation(p.Key,p.Value)).ToArray()));
        foreach(var prefix in new byte[][]{[],[2],[255],[4]})
        foreach(var after in new byte[]?[]{null,[1,255],[2,9],[3],[255,255]})
        {
            var expected=model.Where(p=>p.Key.AsSpan().StartsWith(prefix) && (after==null || NomKeyComparer.Instance.Compare(p.Key,after)>0)).ToArray();
            var read=new List<NomKvPair>(); var cursor=after; var pages=0;
            do
            {
                var page=store.ReadPage(prefix,cursor,maximumItems:3,maximumBytes:8); Assert.Equal(1,page.Sequence); Assert.Equal(1,page.Generation);
                Assert.InRange(page.Items.Count,0,2); Assert.True(page.Items.Sum(p=>p.Key.Length+p.Value.Length)<=8); read.AddRange(page.Items); Assert.True(++pages<50);
                if(!page.HasMore)break; Assert.NotEmpty(page.Items);cursor=page.Items[^1].Key;
            }while(true);
            Assert.Equal(expected.Select(p=>Convert.ToHexString(p.Key)),read.Select(p=>Convert.ToHexString(p.Key))); Assert.Equal(expected.Select(p=>p.Value[0]),read.Select(p=>p.Value[0]));
        }
        var detached=store.ReadPage([2],maximumItems:3); var oldKeys=detached.Items.Select(p=>p.Key.ToArray()).ToArray();
        store.WriteBatch(new(2,1,2,Guid.NewGuid(),[new([2,0],[99]),new([2,1],null),new([2,20],[20])]));
        var changed=store.ReadPage([2],maximumItems:3); Assert.Equal(2,changed.Sequence); Assert.Equal(new byte[]{0,2,3},changed.Items.Select(p=>p.Key[1])); Assert.Equal(99,changed.Items[0].Value[0]);
        Assert.Equal(oldKeys.Select(p=>Convert.ToHexString(p)),detached.Items.Select(p=>Convert.ToHexString(p.Key))); Assert.Equal(0,detached.Items[0].Value[0]);
        changed.Items[0].Value[0]=42; changed.Items[0].Key[0]=88; Assert.Equal(new byte[]{99},store.Get([2,0]));
        store.Compact(); var compacted=store.ReadPage([2],maximumItems:3); Assert.Equal(2,compacted.Sequence); Assert.Equal(new byte[]{0,2,3},compacted.Items.Select(p=>p.Key[1])); Assert.Equal(99,compacted.Items[0].Value[0]);
        store.Dispose(); using var reopened=context.Factory.Open(context.Directory.Arguments()); var replayed=reopened.ReadPage([2],maximumItems:3);
        Assert.Equal(compacted.Items.Select(p=>Convert.ToHexString(p.Key)),replayed.Items.Select(p=>Convert.ToHexString(p.Key))); Assert.Equal(compacted.Items.Select(p=>p.Value[0]),replayed.Items.Select(p=>p.Value[0]));
    }
}
