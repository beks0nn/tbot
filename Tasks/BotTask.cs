using Bot.State;

namespace Bot.Tasks;

public enum TaskStatus { NotStarted, Running, Completed }

/// <summary>
/// Base class for root tasks - scheduled by TaskOrchestrator.
/// Has priority for preemption decisions.
///
/// Lifecycle:
/// 1. OnStart() - called once when task begins
/// 2. Execute() - called every tick while running, call Complete() when done
/// 3. OnComplete() - called once after Complete() is called
/// </summary>
public abstract class BotTask
{
    public abstract int Priority { get; }
    public string Name { get; protected set; } = "";
    public TaskStatus Status { get; private set; } = TaskStatus.NotStarted;
    public bool IsCompleted => Status == TaskStatus.Completed;

    /// <summary>
    /// If true, task cannot be preempted by higher priority tasks.
    /// </summary>
    public virtual bool IsCritical => false;

    /// <summary>
    /// Called once when the task starts.
    /// Can call Fail() to abort immediately.
    /// </summary>
    protected virtual void OnStart(BotContext ctx) { }

    /// <summary>
    /// Called every tick while running.
    /// Call Complete() when done.
    /// </summary>
    protected abstract void Execute(BotContext ctx);

    /// <summary>
    /// Called once when task completes successfully.
    /// Use for cleanup.
    /// </summary>
    protected virtual void OnComplete(BotContext ctx) { }

    /// <summary>
    /// Mark task as completed.
    /// </summary>
    protected void Complete()
    {
        if (Status != TaskStatus.Completed)
            Status = TaskStatus.Completed;
    }

    /// <summary>
    /// Mark task as failed/aborted immediately.
    /// </summary>
    protected void Fail(string? reason = null)
    {
        Status = TaskStatus.Completed;
        if (reason != null)
            Console.WriteLine($"[{Name}] Failed: {reason}");
    }

    /// <summary>
    /// Main tick method - called by orchestrator.
    /// </summary>
    public void Tick(BotContext ctx)
    {
        switch (Status)
        {
            case TaskStatus.NotStarted:
                OnStart(ctx);
                if (Status == TaskStatus.Completed)
                {
                    OnComplete(ctx);
                    return;
                }
                Status = TaskStatus.Running;
                break;

            case TaskStatus.Running:
                Execute(ctx);
                if (Status == TaskStatus.Completed)
                    OnComplete(ctx);
                break;

            case TaskStatus.Completed:
                break;
        }
    }
}
