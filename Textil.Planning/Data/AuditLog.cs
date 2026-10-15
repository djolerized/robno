namespace Textil.Planning.Data;

public class AuditLog
{
    public long Id { get; set; }
    public string Entity { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = "";

    /// <summary>Null for system actions (e.g. automatic lock after deadline).</summary>
    public string? UserId { get; set; }

    public DateTime At { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
