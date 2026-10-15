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
