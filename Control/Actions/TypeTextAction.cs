namespace Bot.Control.Actions;

public sealed class TypeTextAction(KeyMover keyboard, string text, IntPtr handle) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(text.Length * 60 + 75);

    public override async Task RunAsync(CancellationToken ct)
    {
        await keyboard.TypeTextAsync(text, handle, ct);
        await keyboard.PressEnterAsync(handle, ct);
    }
}
