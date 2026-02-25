using Bot.Control;
using Bot.Control.Actions;
using Bot.State;

namespace Bot.Chat;

/// <summary>
/// Single owner of all chat actions: tab detection, tab switching, and response typing.
/// Uses a state machine so tab switches and typing are always one coherent sequence —
/// no race conditions between separate components.
/// </summary>
public sealed class ChatOrchestrator
{
    private const string DefaultTab = "Default";
    private const int TabWidth = 96;

    private enum Phase { Idle, ReadingTab, Responding }

    // --- Tab detection (passive, never enqueues actions) ---
    private readonly TabDetector _tabDetector = new();
    private (int X, int Y)? _defaultTabCenter;
    private int _detectedPrivateTabCount;
    private int _focusedTabIndex = -1; // -1 = Default, 0+ = private tab index
    private int _tabHeight = 20;
    private DateTime _lastTabDetect = DateTime.MinValue;
    private readonly Dictionary<int, DateTime> _slotLastActivity = new();

    // --- Response handling ---
    private readonly AiChatService _aiService = new();
    private readonly Dictionary<string, PlayerConversation> _conversations = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan BatchDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ConversationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResponseCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StaleTabTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReadTabTimeout = TimeSpan.FromSeconds(8);

    private DateTime _lastResponseTime = DateTime.MinValue;

    // --- State machine ---
    private Phase _phase = Phase.Idle;
    private DateTime _phaseStartTime;
    private volatile bool _asyncBusy;   // true while AI is generating
    private ActionHandle? _lastAction;  // last enqueued action, to know when done

    public void Update(BotContext ctx, BotServices svc)
    {
        if (!ctx.Profile.AiChatEnabled)
            return;

        DetectTabs(ctx);
        DrainMessages(ctx);
        CleanupStaleConversations();

        switch (_phase)
        {
            case Phase.Idle:
                OnIdle(ctx, svc);
                break;
            case Phase.ReadingTab:
                OnReadingTab(ctx, svc);
                break;
            case Phase.Responding:
                OnResponding();
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  State handlers
    // ──────────────────────────────────────────────

    private void OnIdle(BotContext ctx, BotServices svc)
    {
        // 1. If on Default and there's an unread private tab, switch to it so ChatReader can read it
        if (_focusedTabIndex == -1)
        {
            var unread = ctx.Chat.Tabs.FirstOrDefault(t => t.IsPrivate && t.IsUnread);
            if (unread != null)
            {
                Console.WriteLine($"[Chat] Switching to unread tab at ({unread.ScreenX},{unread.ScreenY})");
                _lastAction = svc.Queue.Enqueue(
                    new LeftClickScreenAction(svc.Mouse, unread.ScreenX, unread.ScreenY));
                ctx.Chat.ActiveTab = unread.Name;
                _focusedTabIndex = 0;
                EnterPhase(Phase.ReadingTab);
                return;
            }
        }

        // 2. Respond to a ready conversation
        if ((DateTime.UtcNow - _lastResponseTime) >= ResponseCooldown)
        {
            var ready = FindReadyConversation();
            if (ready != null)
            {
                StartResponding(ready, ctx, svc);
                return;
            }
        }

        // 3. Close stale private tabs
        CloseStaleTab(ctx, svc);
    }

    private void OnReadingTab(BotContext ctx, BotServices svc)
    {
        // Wait for the tab-click to execute
        if (_lastAction is { IsCompleted: false })
            return;

        // Check if a private conversation is now ready
        if ((DateTime.UtcNow - _lastResponseTime) >= ResponseCooldown)
        {
            var ready = FindReadyConversation();
            if (ready != null)
            {
                // We're already on the private tab — respond directly
                StartResponding(ready, ctx, svc);
                return;
            }
        }

        // Timeout — return to Default
        if ((DateTime.UtcNow - _phaseStartTime) > ReadTabTimeout)
        {
            Console.WriteLine("[Chat] Tab read timeout, returning to default");
            ClickDefaultTab(ctx, svc);
            EnterPhase(Phase.Idle);
        }
    }

    private void OnResponding()
    {
        if (_asyncBusy)
            return;
        if (_lastAction is { IsCompleted: false })
            return;

        EnterPhase(Phase.Idle);
    }

    // ──────────────────────────────────────────────
    //  Response logic
    // ──────────────────────────────────────────────

    private void StartResponding(PlayerConversation conv, BotContext ctx, BotServices svc)
    {
        Console.WriteLine($"[Chat] Generating response for {conv.PlayerName} ({conv.PendingCount} messages)");
        _asyncBusy = true;
        EnterPhase(Phase.Responding);
        _ = RespondAsync(conv, ctx, svc);
    }

    private async Task RespondAsync(PlayerConversation conv, BotContext ctx, BotServices svc)
    {
        try
        {
            // Snapshot how many messages we're responding to.
            // New messages arriving during LLM generation shouldn't be swallowed.
            int pendingAtStart = conv.PendingCount;
            bool isPrivate = conv.LastMessage?.Type == ChatMessageType.PlayerPrivate;

            // Generate AI response FIRST — before touching the queue.
            var response = await GenerateResponse(conv, ctx, pendingAtStart);
            if (string.IsNullOrWhiteSpace(response))
            {
                if (isPrivate) ClickDefaultTab(ctx, svc);
                return;
            }

            // Now enqueue ALL actions at once (non-async Enqueue) so they sit
            // back-to-back in the queue with no gaps for bot-task actions to slip in.
            if (isPrivate)
                EnqueuePrivateResponse(conv, response, ctx, svc);
            else
                EnqueuePublicResponse(response, ctx, svc);

            _lastResponseTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Chat] Error responding to {conv.PlayerName}: {ex.Message}");
            ClickDefaultTab(ctx, svc);
        }
        finally
        {
            // Signal that AI generation is done; OnResponding will wait for _lastAction.
            _asyncBusy = false;
        }
    }

    private void EnqueuePrivateResponse(
        PlayerConversation conv, string response, BotContext ctx, BotServices svc)
    {
        bool onPrivateTab = ctx.Chat.ActiveTab != DefaultTab;

        if (onPrivateTab)
        {
            // Already on the private tab — just type
            Console.WriteLine($"[Chat] Typing on private tab for {conv.PlayerName}");
            svc.Queue.Enqueue(new TypeTextAction(svc.Keyboard, response, ctx.GameWindowHandle));
        }
        else
        {
            // On Default — right-click message, click "Message To", then type
            var lastMsg = conv.LastMessage!;
            if (lastMsg.ScreenX <= 0 || lastMsg.ScreenY <= 0)
            {
                Console.WriteLine($"[Chat] No screen position for {conv.PlayerName}");
                return;
            }

            Console.WriteLine(
                $"[Chat] Opening private tab for {conv.PlayerName} via right-click at ({lastMsg.ScreenX}, {lastMsg.ScreenY})");

            svc.Queue.Enqueue(new RightClickScreenAction(svc.Mouse, lastMsg.ScreenX, lastMsg.ScreenY));
            svc.Queue.Enqueue(new DelayAction(TimeSpan.FromMilliseconds(500)));

            var cr = ctx.Profile.ChatRect;
            if (ctx.MessageToTemplate != null && cr.IsValid)
                svc.Queue.Enqueue(new FindAndClickTemplateAction(
                    svc.Mouse, ctx.MessageToTemplate, cr.X, cr.Y, cr.W, cr.H));
            else
                svc.Queue.Enqueue(new LeftClickScreenAction(
                    svc.Mouse, lastMsg.ScreenX, lastMsg.ScreenY + 45));

            svc.Queue.Enqueue(new DelayAction(TimeSpan.FromMilliseconds(600)));
            svc.Queue.Enqueue(new TypeTextAction(svc.Keyboard, response, ctx.GameWindowHandle));
        }

        svc.Queue.Enqueue(new DelayAction(TimeSpan.FromMilliseconds(300)));
        _lastAction = ClickDefaultTab(ctx, svc);
    }

    private void EnqueuePublicResponse(string response, BotContext ctx, BotServices svc)
    {
        _lastAction = svc.Queue.Enqueue(
            new TypeTextAction(svc.Keyboard, response, ctx.GameWindowHandle));
    }

    // ──────────────────────────────────────────────
    //  Tab detection (passive — only reads screen, never enqueues)
    // ──────────────────────────────────────────────

    private void DetectTabs(BotContext ctx)
    {
        if (!ctx.Profile.ChatTabsRect.IsValid)
            return;
        if ((DateTime.UtcNow - _lastTabDetect).TotalSeconds < 2)
            return;
        _lastTabDetect = DateTime.UtcNow;

        // Find Default tab position + focus state
        if (ctx.DefaultTabTemplate != null && ctx.DefaultTabUnfocusedTemplate != null)
        {
            var result = _tabDetector.FindDefaultTab(
                ctx.CurrentFrameGray, ctx.Profile.ChatTabsRect,
                ctx.DefaultTabTemplate, ctx.DefaultTabUnfocusedTemplate);

            if (result.HasValue)
            {
                _defaultTabCenter = (result.Value.ScreenX, result.Value.ScreenY);
                if (result.Value.IsFocused)
                    _focusedTabIndex = -1;
                _tabHeight = ctx.DefaultTabTemplate.Height;
            }
        }

        // Count private tabs
        if (_defaultTabCenter.HasValue &&
            ctx.TabLeftEdgeTemplate != null && ctx.TabLeftEdgeUnfocusedTemplate != null)
        {
            var (count, focusedPrivate) = _tabDetector.CountPrivateTabs(
                ctx.CurrentFrameGray, ctx.Profile.ChatTabsRect,
                _defaultTabCenter.Value.X, TabWidth,
                ctx.TabLeftEdgeTemplate, ctx.TabLeftEdgeUnfocusedTemplate);

            _detectedPrivateTabCount = count;
            if (focusedPrivate >= 0)
                _focusedTabIndex = focusedPrivate;
        }

        // Build tabs list
        var tabs = new List<ChatTab>();
        if (_defaultTabCenter.HasValue)
        {
            tabs.Add(new ChatTab
            {
                Name = DefaultTab,
                IsPrivate = false,
                IsUnread = false,
                ScreenX = _defaultTabCenter.Value.X,
                ScreenY = _defaultTabCenter.Value.Y
            });

            for (int i = 0; i < _detectedPrivateTabCount; i++)
            {
                int slotX = _defaultTabCenter.Value.X + (i + 1) * TabWidth;
                int slotY = _defaultTabCenter.Value.Y;

                bool hasRed = _tabDetector.HasRedPixelsInSlot(
                    ctx.CurrentFrame, ctx.Profile.ChatTabsRect, slotX, TabWidth, _tabHeight);

                tabs.Add(new ChatTab
                {
                    Name = $"Private-{i + 1}",
                    IsPrivate = true,
                    IsUnread = hasRed,
                    ScreenX = slotX,
                    ScreenY = slotY
                });

                if (hasRed)
                    _slotLastActivity[slotX] = DateTime.UtcNow;
            }
        }

        ctx.Chat.Tabs = tabs;
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private ActionHandle? ClickDefaultTab(BotContext ctx, BotServices svc)
    {
        if (!_defaultTabCenter.HasValue)
        {
            // Try to detect if not cached
            if (ctx.DefaultTabTemplate != null && ctx.DefaultTabUnfocusedTemplate != null)
            {
                var result = _tabDetector.FindDefaultTab(
                    ctx.CurrentFrameGray, ctx.Profile.ChatTabsRect,
                    ctx.DefaultTabTemplate, ctx.DefaultTabUnfocusedTemplate);

                if (result.HasValue)
                    _defaultTabCenter = (result.Value.ScreenX, result.Value.ScreenY);
            }
        }

        if (_defaultTabCenter.HasValue)
        {
            ctx.Chat.ActiveTab = DefaultTab;
            _focusedTabIndex = -1;
            return svc.Queue.Enqueue(new LeftClickScreenAction(
                svc.Mouse, _defaultTabCenter.Value.X, _defaultTabCenter.Value.Y));
        }

        Console.WriteLine("[Chat] Cannot switch to Default: position unknown");
        return null;
    }

    private void CloseStaleTab(BotContext ctx, BotServices svc)
    {
        var staleTabs = ctx.Chat.Tabs
            .Where(t => t.IsPrivate)
            .Where(t =>
            {
                if (_slotLastActivity.TryGetValue(t.ScreenX, out var last))
                    return (DateTime.UtcNow - last) > StaleTabTimeout;
                return false;
            })
            .OrderByDescending(t => t.ScreenX)
            .ToList();

        foreach (var tab in staleTabs)
        {
            Console.WriteLine($"[Chat] Closing stale tab at ({tab.ScreenX},{tab.ScreenY})");
            svc.Queue.Enqueue(new LeftClickScreenAction(svc.Mouse, tab.ScreenX, tab.ScreenY));
            svc.Queue.Enqueue(new DelayAction(TimeSpan.FromMilliseconds(300)));

            var tr = ctx.Profile.ChatTabsRect;
            if (ctx.CloseTabTemplate != null && tr.IsValid)
                svc.Queue.Enqueue(new FindAndClickTemplateAction(
                    svc.Mouse, ctx.CloseTabTemplate, tr.X, tr.Y, tr.W, tr.H));

            _slotLastActivity.Remove(tab.ScreenX);
        }
    }

    private PlayerConversation? FindReadyConversation()
    {
        foreach (var conv in _conversations.Values)
        {
            if (conv.NeedsResponse && (DateTime.UtcNow - conv.LastPlayerMessageTime) > BatchDelay)
                return conv;
        }
        return null;
    }

    private void EnterPhase(Phase phase)
    {
        _phase = phase;
        _phaseStartTime = DateTime.UtcNow;
    }

    private void DrainMessages(BotContext ctx)
    {
        while (ctx.Chat.UnhandledPlayerMessages.TryDequeue(out var msg))
        {
            if (msg.Type == ChatMessageType.System)
                continue;
            if (msg.SenderName.Equals(ctx.Profile.PlayerName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsSpell(msg.Content))
                continue;

            var conv = GetOrCreate(msg.SenderName);
            conv.AddPlayerMessage(msg);
            Console.WriteLine($"[Chat] Queued from {msg.SenderName}: \"{msg.Content}\" ({conv.PendingCount} pending)");
        }
    }

    private void CleanupStaleConversations()
    {
        var stale = _conversations
            .Where(kv => (DateTime.UtcNow - kv.Value.LastActivityTime) > ConversationTimeout)
            .Select(kv => kv.Key).ToList();
        foreach (var key in stale)
            _conversations.Remove(key);
    }

    private async Task<string?> GenerateResponse(PlayerConversation conv, BotContext ctx, int pendingAtStart)
    {
        var apiMessages = conv.BuildApiMessages();

        var response = await _aiService.GenerateResponseAsync(
            ctx.Profile.AiChatContext, apiMessages, CancellationToken.None);

        if (string.IsNullOrWhiteSpace(response))
        {
            Console.WriteLine($"[Chat] No response generated for {conv.PlayerName}");
            return null;
        }

        if (response.Length > 200)
            response = response[..200];

        conv.AddBotResponse(response, pendingAtStart);
        Console.WriteLine($"[Chat] Responded to {conv.PlayerName}: {response}");
        return response;
    }

    private PlayerConversation GetOrCreate(string playerName)
    {
        if (!_conversations.TryGetValue(playerName, out var conv))
        {
            conv = new PlayerConversation(playerName);
            _conversations[playerName] = conv;
        }
        return conv;
    }

    // ──────────────────────────────────────────────
    //  Spell filtering
    // ──────────────────────────────────────────────

    private static bool IsSpell(string content)
    {
        var trimmed = content.Trim();
        if (SpellIncantations.Contains(trimmed))
            return true;

        foreach (var prefix in SpellPrefixesWithTarget)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly string[] SpellPrefixesWithTarget =
    [
        "exura sio ",
        "exiva ",
        "utevo res ina ",
        "adeta sio "
    ];

    private static readonly HashSet<string> SpellIncantations = new(StringComparer.OrdinalIgnoreCase)
    {
        "exevo grav vita", "exana mas mort", "utevo vis lux", "adura vita",
        "exura vita", "exevo gran mas vis", "utevo res para", "adori vita vis",
        "utani gran hur", "adevo res flam", "exevo con vis", "exevo con pox",
        "adevo mas pox", "adevo mas grav pox", "exevo gran mas pox", "adevo grav pox",
        "adana ani", "exura gran mas res", "adevo grav tera", "utamo vita",
        "exani tera", "adori", "exura", "utevo lux", "exani hur para",
        "utana vid", "adura gran", "exura gran", "adori gran", "exura sio para",
        "utani hur", "utevo gran lux", "adori gran flam", "exevo gran vis lux",
        "exori mort", "exevo pan", "exori flam", "adevo mas flam", "adori flam",
        "exevo flam hur", "adevo mas grav flam", "adevo grav flam", "exiva para",
        "exana flam", "exevo con flam", "adevo mas hur", "adevo res pox",
        "adevo mas vis", "exevo mort hur", "adevo mas grav vis", "exori vis",
        "adevo grav vis", "exevo vis lux", "exeta vis", "exana vis",
        "adito grav", "exito tera", "adito tera", "utevo res ina para",
        "adeta sio", "exevo con mort", "exevo con", "adevo ina",
        "exeta res", "exana ina", "exori", "adana pox", "exana pox", "adana mort"
    };
}

/// <summary>
/// Tracks a conversation with a single player, including history of both sides.
/// </summary>
internal sealed class PlayerConversation(string playerName)
{
    private const int MaxHistory = 20;

    public string PlayerName { get; } = playerName;
    public DateTime LastPlayerMessageTime { get; private set; }
    public DateTime LastActivityTime { get; private set; }
    public bool NeedsResponse { get; private set; }
    public int PendingCount { get; private set; }
    public ChatMessage? LastMessage { get; private set; }

    private readonly List<(string Role, string Text, DateTime Time)> _history = [];

    public void AddPlayerMessage(ChatMessage msg)
    {
        _history.Add(("player", msg.Content, DateTime.UtcNow));
        LastPlayerMessageTime = DateTime.UtcNow;
        LastActivityTime = DateTime.UtcNow;
        LastMessage = msg;
        NeedsResponse = true;
        PendingCount++;
        TrimHistory();
    }

    public void AddBotResponse(string response, int respondedCount)
    {
        _history.Add(("bot", response, DateTime.UtcNow));
        LastActivityTime = DateTime.UtcNow;
        // Only clear the messages we actually responded to.
        // If new messages arrived during LLM generation, keep them pending.
        PendingCount = Math.Max(0, PendingCount - respondedCount);
        NeedsResponse = PendingCount > 0;
        TrimHistory();
    }

    public List<ConversationTurn> BuildApiMessages()
    {
        var result = new List<ConversationTurn>();
        string? pendingUser = null;

        foreach (var (role, text, _) in _history)
        {
            if (role == "player")
            {
                pendingUser = pendingUser == null
                    ? $"{PlayerName}: {text}"
                    : $"{pendingUser}\n{PlayerName}: {text}";
            }
            else
            {
                if (pendingUser != null)
                {
                    result.Add(new ConversationTurn("user", pendingUser));
                    pendingUser = null;
                }
                result.Add(new ConversationTurn("assistant", text));
            }
        }

        if (pendingUser != null)
            result.Add(new ConversationTurn("user", pendingUser));

        return result;
    }

    private void TrimHistory()
    {
        while (_history.Count > MaxHistory)
            _history.RemoveAt(0);
    }
}
