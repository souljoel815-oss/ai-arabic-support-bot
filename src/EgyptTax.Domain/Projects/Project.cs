using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Projects;

/// <summary>
/// v3 §11 #9 — project / engagement aggregate. Consulting offices,
/// agencies, construction firms scope work into projects with
/// start/end dates, optional customer link, and a budget.
///
/// v1 ships projects + tasks + status. Timesheet entry + project
/// P&L (revenue from invoices tagged to project minus expenses
/// tagged to project) are deferred — they need invoice CC tagging
/// (see Cost centers v1 limit). When invoice tagging lands, this
/// gets P&L automatically by joining on project_id.
/// </summary>
public sealed class Project
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = "";
    public ArabicEnglishText Name { get; private set; }
    public Guid? CustomerId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public decimal? BudgetEgp { get; private set; }
    public ProjectStatus Status { get; private set; } = ProjectStatus.Planning;

    private readonly List<ProjectTask> _tasks = new();
    public IReadOnlyCollection<ProjectTask> Tasks => _tasks;

    private Project() { }

    public Project(string code, ArabicEnglishText name, DateOnly startDate, Guid? customerId = null,
        DateOnly? endDate = null, decimal? budgetEgp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (endDate is { } e && e < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));
        if (budgetEgp is < 0) throw new ArgumentOutOfRangeException(nameof(budgetEgp));
        Code = code.Trim();
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        CustomerId = customerId;
        BudgetEgp = budgetEgp;
    }

    public void Update(ArabicEnglishText name, Guid? customerId, DateOnly startDate,
        DateOnly? endDate, decimal? budgetEgp)
    {
        if (endDate is { } e && e < startDate)
            throw new ArgumentException("End date cannot be before start date.");
        Name = name;
        CustomerId = customerId;
        StartDate = startDate;
        EndDate = endDate;
        BudgetEgp = budgetEgp;
    }

    public void Activate() => Status = ProjectStatus.Active;
    public void PutOnHold() => Status = ProjectStatus.OnHold;
    public void Complete() => Status = ProjectStatus.Completed;
    public void Cancel() => Status = ProjectStatus.Cancelled;

    public ProjectTask AddTask(
        string title,
        Guid? assignedToUserId,
        DateOnly? dueDate,
        TaskPriority priority = TaskPriority.Normal,
        Guid? parentTaskId = null)
    {
        var task = new ProjectTask(Id, title, assignedToUserId, dueDate, priority, parentTaskId);
        _tasks.Add(task);
        return task;
    }
}

public enum ProjectStatus
{
    Planning = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4,
}
