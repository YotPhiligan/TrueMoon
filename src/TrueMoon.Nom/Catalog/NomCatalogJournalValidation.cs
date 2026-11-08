using System.Globalization;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

internal sealed partial class NomCatalog
{
    // Read-only closure check shared by transport verification, without recovery/abort/staging cleanup.
    internal void ValidateJournal()
    {
        var header=Header; var control=Control;
        foreach(var op in Operations())
        {
            NomCatalogRecordCodec.ValidateOperation(op);
            NomBinary.Require(op.Sequence>control.Floor && op.Sequence<control.Next && op.Expected<=header.Generation,"Journal sequence/generation integrity.");
            var items=Items(op.Sequence); NomBinary.Require(items.Length==op.Count,"Operation item closure.");
            var fields=new List<string> { "TrueMoon.Nom/request/1",header.StoreId.ToString("N"),op.Expected.ToString(CultureInfo.InvariantCulture),op.Total.ToString(CultureInfo.InvariantCulture) };
            var ids=new List<Guid>(); long known=0,total=0;
            for(var i=0;i<items.Length;i++)
            {
                var item=items[i]; NomBinary.Require(item.Sequence==op.Sequence && item.Index==i && (op.State=="committed" ? item.Revision!=null : item.Revision==null),"Operation item state/order.");
                ids.Add(CatalogCodec.Decode(item.Asset).Id); if(item.ExpectedLength is { } length) known=checked(known+length);
                fields.AddRange([ResourceCodec.Hash(item.Asset),item.Maximum.ToString(CultureInfo.InvariantCulture),item.ExpectedLength?.ToString(CultureInfo.InvariantCulture)??"unknown",item.ExpectedDigest??"unknown"]);
                if(op.State!="committed") continue;
                var record=Revision(item.Revision!)??throw new InvalidDataException("Missing journal revision."); var version=record.Version;
                NomBinary.Require(record.Asset.AsSpan().SequenceEqual(item.Asset) && version.Length<=item.Maximum && (item.ExpectedLength==null || version.Length==item.ExpectedLength) &&
                    (item.ExpectedDigest==null || version.Digest==item.ExpectedDigest),"Journal output integrity."); total=checked(total+version.Length);
            }
            NomBinary.Require(known<=op.Total && total<=op.Total && ids.Distinct().Count()==ids.Count && ids.SequenceEqual(ids.Order()) && CatalogCodec.Key(fields.ToArray())==op.Request &&
                (op.State!="committed" || op.Result<=header.Generation),"Journal request/output closure.");
        }
    }
}
