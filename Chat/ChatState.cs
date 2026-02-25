using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Bot.Chat;

public sealed class ChatState
{
    private const int MaxHistory = 200;
    private readonly HashSet<string> _seenHashes = new();

    public List<ChatMessage> RecentMessages { get; } = [];
    public Queue<ChatMessage> MessageHistory { get; } = new();
    public List<ChatTab> Tabs { get; set; } = [];
    public string ActiveTab { get; set; } = "Default";
    public ConcurrentQueue<ChatMessage> UnhandledPlayerMessages { get; } = new();

    public bool TryAddMessage(ChatMessage msg, string playerName = "")
    {
        var hash = ComputeHash(msg.RawText);
        if (!_seenHashes.Add(hash))
            return false;

        bool isOwnMessage = !string.IsNullOrEmpty(playerName) &&
            msg.SenderName.Equals(playerName, StringComparison.OrdinalIgnoreCase);

        RecentMessages.Add(msg);
        MessageHistory.Enqueue(msg);

        while (MessageHistory.Count > MaxHistory)
        {
            var old = MessageHistory.Dequeue();
            _seenHashes.Remove(ComputeHash(old.RawText));
        }

        var typeTag = msg.Type switch
        {
            ChatMessageType.PlayerPrivate => "PM",
            ChatMessageType.PlayerPublic => "Public",
            _ => "System"
        };

        if (!string.IsNullOrEmpty(msg.SenderName))
            Console.WriteLine($"[Chat] [{typeTag}]{(isOwnMessage ? " (self)" : "")} {msg.SenderName}: {msg.Content}");
        else
            Console.WriteLine($"[Chat] [{typeTag}] {msg.Content}");

        // Only queue OTHER players' messages for AI response
        if (!isOwnMessage && msg.Type is ChatMessageType.PlayerPublic or ChatMessageType.PlayerPrivate)
            UnhandledPlayerMessages.Enqueue(msg);

        return true;
    }

    public void ClearRecent()
    {
        RecentMessages.Clear();
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes, 0, 8);
    }
}
