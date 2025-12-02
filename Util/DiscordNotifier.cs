using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Bot.Util;

public static class DiscordNotifier
{
    private static readonly HttpClient _http = new();
    private const string webhookUrl = "";

    public static async Task SendAsync(string message)
    {
        var payload = new { content = message };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _http.PostAsync(webhookUrl, content);
    }
}

