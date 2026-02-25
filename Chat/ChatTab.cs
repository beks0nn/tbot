namespace Bot.Chat;

public sealed class ChatTab
{
    public required string Name { get; set; }
    public bool IsUnread { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime LastMessageAt { get; set; }
    public int ScreenX { get; set; }
    public int ScreenY { get; set; }
}
