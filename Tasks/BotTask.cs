using Bot.State;

namespace Bot.Tasks;

public enum TaskStatus { NotStarted, Running, AwaitingDelay, Completed }

public abstract class BotTask
{
    public abstract int Priority { get; }
    public string Name { get; protected set; } = "";
    public TaskStatus Status { get; set; } = TaskStatus.NotStarted;
    public DateTime StartedAt { get; private set; }
    public TimeSpan DelayAfterComplete { get; set; } = TimeSpan.Zero;
    public virtual bool IsCritical => false;

    public virtual void Tick(BotContext ctx)
    {
        switch (Status)
        {
            case TaskStatus.NotStarted:
                OnBeforeStart(ctx);
                StartedAt = DateTime.UtcNow;
                Status = TaskStatus.Running;
                break;

            case TaskStatus.Running:
                Do(ctx);
                if (Did(ctx))
                {
                    Status = DelayAfterComplete > TimeSpan.Zero
                        ? TaskStatus.AwaitingDelay
                        : TaskStatus.Completed;
                }
                break;

            case TaskStatus.AwaitingDelay:
                if (DateTime.UtcNow - StartedAt >= DelayAfterComplete)
                    Status = TaskStatus.Completed;
                break;

            case TaskStatus.Completed:
                break;
        }
    }

    public bool IsCompleted => Status == TaskStatus.Completed;

    public abstract void OnBeforeStart(BotContext ctx);
    public abstract void Do(BotContext ctx);
    public abstract bool Did(BotContext ctx);
}
