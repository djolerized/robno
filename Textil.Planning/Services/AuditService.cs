using System.Text.Json;
using Textil.Planning.Data;

namespace Textil.Planning.Services;

public interface IAuditService
{
    /// <summary>Adds an audit entry to the current unit of work; it is persisted by the caller's SaveChanges.</summary>
    void Add(string entity, string entityId, string action, string? userId, object? oldValue, object? newValue);
}

public class AuditService(AppDbContext db, TimeProvider clock) : IAuditService
{
    public void Add(string entity, string entityId, string action, string? userId, object? oldValue, object? newValue)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Entity = entity,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            At = clock.GetUtcNow().UtcDateTime,
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue),
        });
    }
}
