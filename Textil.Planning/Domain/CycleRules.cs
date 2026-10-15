using Textil.Planning.Data;

namespace Textil.Planning.Domain;

public static class CycleRules
{
    /// <summary>
    /// Writes are allowed only while the cycle is Open and before the deadline. The deadline is checked here as well,
    /// so a write cannot slip through before the background job locks the cycle.
    /// </summary>
    public static bool IsWritable(Cycle cycle, DateTime nowUtc) =>
        cycle.Status == CycleStatus.Open && cycle.Deadline > nowUtc;

    public static void EnsureWritable(Cycle cycle, DateTime nowUtc)
    {
        if (cycle.Status != CycleStatus.Open)
            throw new DomainException("Ciklus nije otvoren — izmene nisu moguće.");
        if (cycle.Deadline <= nowUtc)
            throw new DomainException("Rok za unos je istekao — izmene nisu moguće.");
    }
}

/// <summary>How procurement sees a cycle: new (nothing sent yet), in progress, or closed (finalized).</summary>
public enum ProcurementStage
{
    New,
    InProgress,
    Closed,
}

public static class ProcurementStages
{
    /// <summary>
    /// A cycle is new for procurement while it is Open and has no batch. A Locked cycle can still be unlocked,
    /// so it stays in progress; only Finalized is closed.
    /// </summary>
    public static ProcurementStage Of(CycleStatus status, int batchCount) => status switch
    {
        CycleStatus.Finalized => ProcurementStage.Closed,
        CycleStatus.Open when batchCount == 0 => ProcurementStage.New,
        _ => ProcurementStage.InProgress,
    };
}
