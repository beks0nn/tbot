using Bot.Tasks;

public sealed class TaskOrchestrator
{
    private BotTask? _rootTask;

    public BotTask? Current => _rootTask;

    public void MaybeReplaceRoot(BotTask? candidate)
    {
        // nothing new proposed
        if (candidate == null)
            return;

        // if no task or completed -> accept
        if (_rootTask == null || _rootTask.IsCompleted)
        {
            SetRoot(candidate);
            return;
        }

        // skip replacement if current is still running or awaiting delay
        if (_rootTask.Status == TaskStatus.Running || _rootTask.Status == TaskStatus.AwaitingDelay)
            return;

        // if priority higher, replace
        if (candidate.Priority > _rootTask.Priority)
            SetRoot(candidate);
    }

    private void SetRoot(BotTask? task)
    {
        if (_rootTask != null)
            Console.WriteLine($"[Orchestrator] ✋ Stopping current root task: {_rootTask.Name}");

        if (task != null)
            Console.WriteLine($"[Orchestrator] 🧠 New root task: {task.Name}");

        _rootTask = task;
    }

    public void Tick(BotContext ctx)
    {
        if (_rootTask == null)
            return;

        _rootTask.Tick(ctx);

        if (_rootTask.IsCompleted)
        {
            Console.WriteLine($"[Orchestrator] ✅ Root task '{_rootTask.Name}' completed.");
            _rootTask = null;
        }
    }

    public void Reset()
    {
        if (_rootTask != null)
        {
            Console.WriteLine($"[Orchestrator] 🔄 Resetting orchestrator (clearing '{_rootTask.Name}').");
            _rootTask = null;
        }
    }
}
