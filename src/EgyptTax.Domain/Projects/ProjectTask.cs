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

    /// <summary>v5 D.2.3 — earliest day the task is scheduled to start.
    /// Nullable: tasks without a start date are skipped from the
    /// Gantt chart (still visible on the kanban board). When set,
    /// must be ≤ <see cref="DueDate"/> if both are populated.</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>v5 A.3 — task priority. Drives the kanban-column
    /// sort (Urgent first, then High, Normal, Low) so the most
    /// important work surfaces at the top of each column. Defaults
    /// to Normal so pre-v5 rows render unchanged after migration.</summary>
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;

    /// <summary>v5 A.3 — single-level sub-task parent. Null for
    /// top-level tasks; non-null for tasks that nest one level
    /// under a parent. Multi-level trees intentionally out of
    /// scope (one level covers the common case + keeps the kanban
    /// + Gantt rendering simple).</summary>
    public Guid? ParentTaskId { get; private set; }

    private ProjectTask() { }

    public ProjectTask(
        Guid projectId,
        string title,
        Guid? assignedToUserId,
        DateOnly? dueDate,
        TaskPriority priority = TaskPriority.Normal,
        Guid? parentTaskId = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId required.", nameof(projectId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ProjectId = projectId;
        Title = title.Trim();
        AssignedToUserId = assignedToUserId;
        DueDate = dueDate;
        Priority = priority;
        ParentTaskId = parentTaskId == Guid.Empty ? null : parentTaskId;
    }

    public void Update(
        string title,
        Guid? assignedToUserId,
        DateOnly? dueDate,
        TaskPriority? priority = null,
        Guid? parentTaskId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        AssignedToUserId = assignedToUserId;
        DueDate = dueDate;
        if (priority is { } p) Priority = p;
        // Setting parent to Guid.Empty explicitly clears it; null
        // means "leave as-is" (caller didn't supply).
        if (parentTaskId is { } np)
        {
            ParentTaskId = np == Guid.Empty ? null : np;
        }
    }

    /// <summary>v5 D.2.3 — set the planned schedule range. Pass
    /// nullables to clear; both populated must be in chronological
    /// order.</summary>
    public void SetSchedule(DateOnly? startDate, DateOnly? dueDate)
    {
        if (startDate is { } s && dueDate is { } d && s > d)
        {
            throw new ArgumentException(
                $"StartDate {s:yyyy-MM-dd} cannot be after DueDate {d:yyyy-MM-dd}.");
        }
        StartDate = startDate;
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

/// <summary>v5 A.3 — task priority. Numeric ordering matches sort
/// order (Urgent renders first within a kanban column).</summary>
public enum TaskPriority
{
    Urgent = 0,
    High = 1,
    Normal = 2,
    Low = 3,
}
