namespace Textil.Planning.Data;

public enum ApprovalStatus { Draft, Submitted, Returned, Approved }

/// <summary>Approval state of one planner's plan for one batch.</summary>
public class Approval
{
    public int Id { get; set; }
    public int BatchId { get; set; }

    /// <summary>Owner of the plan.</summary>
    public string UserId { get; set; } = "";

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Draft;

    /// <summary>Null when created by the system together with the batch.</summary>
    public string? ChangedById { get; set; }

    public DateTime ChangedAt { get; set; }
    public string? Note { get; set; }
}
