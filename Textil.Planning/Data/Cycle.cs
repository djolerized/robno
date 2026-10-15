namespace Textil.Planning.Data;

public enum CycleStatus { Open, Locked, Finalized }

public class Cycle
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>UTC.</summary>
    public DateTime Deadline { get; set; }

    public CycleStatus Status { get; set; } = CycleStatus.Open;
    public string CreatedById { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? FinalizedAt { get; set; }

    /// <summary>Empty until the first batch defines the months.</summary>
    public List<CycleMonth> Months { get; set; } = [];

    public List<Batch> Batches { get; set; } = [];
}

public class CycleMonth
{
    public int CycleId { get; set; }

    /// <summary>YYYY-MM.</summary>
    public string Month { get; set; } = "";

    public int Order { get; set; }
}
