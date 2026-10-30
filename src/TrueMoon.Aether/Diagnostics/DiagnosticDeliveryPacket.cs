namespace TrueMoon.Aether.Diagnostics;

/// <summary>Fixed identity envelope. Nonce and endpoint must never be copied into diagnostic records.</summary>
public sealed class DiagnosticDeliveryPacket
{
    /// <summary>Body schema:1 is a record array;2 adds a bounded runtime observation in the same byte budget.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Expected engine session.</summary>
    public Guid SessionId { get; set; }
    /// <summary>Expected generation's exact UInt64 bit pattern in the existing Int64 codec.</summary>
    public long Generation { get; set; }
    /// <summary>Exact host identity.</summary>
    public string HostContractId { get; set; } = "";
    /// <summary>Exact game/preview identity.</summary>
    public string GameBuildId { get; set; } = "";
    /// <summary>Private launch capability, never logged.</summary>
    public string Nonce { get; set; } = "";
    /// <summary>RuntimeRole numeric value.</summary>
    public int Role { get; set; }
    /// <summary>Owned child's process identity.</summary>
    public int ProcessId { get; set; }
    /// <summary>One child collector per lane.</summary>
    public Guid ApplicationId { get; set; }
    /// <summary>Monotonic packet sequence; duplicates are refused.</summary>
    public long PacketSequence { get; set; }
    /// <summary>Declared count, validated before and after JSON decoding.</summary>
    public int RecordCount { get; set; }
    /// <summary>Child's cumulative known loss.</summary>
    public long Lost { get; set; }
    /// <summary>Child's cumulative unknown delivery outcomes.</summary>
    public long Unconfirmed { get; set; }
    /// <summary>Bounded JSON body; its schema is selected by Version. The binary envelope is unchanged.</summary>
    public byte[] Payload { get; set; } = [];
}
