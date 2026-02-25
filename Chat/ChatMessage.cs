namespace Bot.Chat;

public enum ChatMessageType
{
    System,
    PlayerPublic,
    PlayerPrivate
}

public sealed class ChatMessage
{
    public required string RawText { get; init; }
    public required string SenderName { get; init; }
    public required string Content { get; init; }
    public required ChatMessageType Type { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Approximate screen X coordinate (center of chat area). Used for right-click context menus.
    /// </summary>
    public int ScreenX { get; init; }

    /// <summary>
    /// Approximate screen Y coordinate of this message line. Used for right-click context menus.
    /// </summary>
    public int ScreenY { get; init; }
}
