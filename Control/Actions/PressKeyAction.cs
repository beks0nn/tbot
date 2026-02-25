namespace Bot.Control.Actions;

public sealed class PressKeyAction(KeyMover keyboard, ushort vk, IntPtr handle) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(75);

    public override async Task RunAsync(CancellationToken ct)
    {
        await keyboard.PressKeyAsync(vk, handle, ct);
    }
}
