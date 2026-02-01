using Bot.State;

namespace Bot.Tasks;

public enum SubTaskStatus { NotStarted, Running, Completed }

/// <summary>
/// Base class for subtasks - simpler lifecycle, used by parent tasks only.
/// Not scheduled by orchestrator directly.
///
/// Lifecycle:
/// 1. OnStart() - called once when subtask begins
/// 2. Execute() - called every tick while running, call Complete() when done
/// 3. OnFinish() - called once after Complete() or Fail() is called
/// </summary>
public abstract class SubTask
{
    public string Name { get; protected set; } = "";
    public SubTaskStatus Status { get; private set; } = SubTaskStatus.NotStarted;
    public bool IsCompleted => Status == SubTaskStatus.Completed;
    public bool Failed { get; private set; }

    /// <summary>
    /// Called once when the subtask starts.
    /// </summary>
    protected virtual void OnStart(BotContext ctx) { }

    /// <summary>
    /// Called every tick while running.
    /// Call Complete() or Fail() when done.
    /// </summary>
    protected abstract void Execute(BotContext ctx);

    /// <summary>
    /// Called once when subtask completes (success or failure).
    /// </summary>
    protected virtual void OnFinish(BotContext ctx) { }

    /// <summary>
    /// Mark subtask as completed successfully.
    /// </summary>
    protected void Complete()
    {
        if (Status != SubTaskStatus.Completed)
            Status = SubTaskStatus.Completed;
    }

    /// <summary>
    /// Mark subtask as completed with failure.
    /// </summary>
    protected void Fail(string? reason = null)
    {
        Failed = true;
        Status = SubTaskStatus.Completed;
        if (reason != null)
            Console.WriteLine($"[{Name}] Failed: {reason}");
    }

    /// <summary>
    /// Main tick method - called by parent task.
    /// </summary>
    public void Tick(BotContext ctx)
    {
        switch (Status)
        {
            case SubTaskStatus.NotStarted:
                OnStart(ctx);
                if (Status == SubTaskStatus.Completed)
                {
                    OnFinish(ctx);
                    return;
                }
                Status = SubTaskStatus.Running;
                break;

            case SubTaskStatus.Running:
                Execute(ctx);
                if (Status == SubTaskStatus.Completed)
                    OnFinish(ctx);
                break;

            case SubTaskStatus.Completed:
                break;
        }
    }
}
