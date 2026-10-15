namespace Textil.Planning.Data;

/// <summary>One SKU upload from procurement into a cycle.</summary>
public class Batch
{
    public int Id { get; set; }
    public int CycleId { get; set; }
    public Cycle? Cycle { get; set; }

    /// <summary>Sequence number within the cycle, starting at 1.</summary>
    public int Number { get; set; }

    public string UploadedById { get; set; } = "";
    public User? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public string OriginalFileName { get; set; } = "";
    public byte[] OriginalFile { get; set; } = [];

    public List<CycleSku> Skus { get; set; } = [];
    public List<Approval> Approvals { get; set; } = [];
}

public class CycleSku
{
    public const int CodeMaxLength = 100;

    public int Id { get; set; }
    public int CycleId { get; set; }
    public int BatchId { get; set; }
    public string Code { get; set; } = "";

    /// <summary>Position within the cycle, continuing across batches in upload order.</summary>
    public int Order { get; set; }
}
