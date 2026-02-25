namespace Bot.Control;

public sealed class ActionHandle
{
    private volatile bool _completed;
    private volatile bool _cancelled;

    public bool IsCompleted => _completed || _cancelled;

    internal void MarkCompleted() => _completed = true;
    internal void MarkCancelled() => _cancelled = true;
}
