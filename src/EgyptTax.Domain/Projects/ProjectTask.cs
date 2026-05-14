namespace EgyptTax.Domain.Projects;

/// <summary>
/// v3 §11 #9 — task within a project. Minimal fields: title +
/// status + assignee + due date. Comments / time logging /
/// dependencies are deferred (v4 trigger).
/// </summary>
public sealed class ProjectTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; init; }
    public string Title { get; private set; } = "";
    public TaskStatus_ Status { get; private set; } = TaskStatus_.Todo;
    public Guid? AssignedToUserId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }

    private ProjectTask() { }

    public ProjectTask(Guid projectId, string title, Guid? assignedToUserId, DateOnly? dueDate)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId required.", nameof(projectId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ProjectId = projectId;
        Title = title.Trim();
        AssignedToUserId = assignedToUserId;
        DueDate = dueDate;
    }

    public void Update(string title, Guid? assignedToUserId, DateOnly? dueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        AssignedToUserId = assignedToUserId;
        DueDate = dueDate;
    }

    public void MoveTo(TaskStatus_ status)
    {
        if (status == Status) return;
        Status = status;
        if (status == TaskStatus_.Done) CompletedAtUtc = DateTime.UtcNow;
        else if (CompletedAtUtc is not null && status != TaskStatus_.Done) CompletedAtUtc = null;
    }
}

/// <summary>Task status. Trailing underscore avoids clashing with
/// System.Threading.Tasks.TaskStatus when the namespace is in scope.</summary>
public enum TaskStatus_
{
    Todo = 0,
    InProgress = 1,
    Blocked = 2,
    Done = 3,
}
