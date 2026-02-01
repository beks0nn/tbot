using Bot.State;

namespace Bot.Tasks;

public sealed class TaskOrchestrator
{
    private BotTask? _rootTask;

    public BotTask? Current => _rootTask;

    public void MaybeReplaceRoot(BotTask? candidate, BotContext ctx)
    {
        if (candidate == null)
            return;

        if (_rootTask != null && _rootTask.IsCritical && !_rootTask.IsCompleted)
            return;

        if (_rootTask == null || _rootTask.IsCompleted)
        {
            _rootTask = candidate;
            return;
        }

        if (candidate.Priority > _rootTask.Priority)
            _rootTask = candidate;
    }

    public void Tick(BotContext ctx)
    {
        if (_rootTask == null)
            return;

        _rootTask.Tick(ctx);

        if (_rootTask.IsCompleted)
            _rootTask = null;
    }
}
