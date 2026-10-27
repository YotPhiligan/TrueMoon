namespace TrueMoon.Aether.Diagnostics;

/// <summary>Admission counts; status0=processed,1=invalid,2=foreign,3=stale,4=rate limited,5=closed.</summary>
public sealed class DiagnosticDeliveryReply
{
    /// <summary>Packet result code.</summary>
    public int Status { get; set; }
    /// <summary>Records imported into host history.</summary>
    public int Accepted { get; set; }
    /// <summary>Records not imported.</summary>
    public int Rejected { get; set; }
    /// <summary>Highest child record sequence consumed, including rejected records.</summary>
    public long LastSequence { get; set; }
}
