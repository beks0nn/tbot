using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Bot.Util;

public static class DiscordNotifier
{
    private static readonly HttpClient _http = new();
    private const string webhookUrl = "https://discordapp.com/api/webhooks/1438230179477721220/FbeCYy980hLYJ4nChXKq2m7BxLJOerrWDvNLJI3aaTAWRXB_TSvHAn7jfDuqxTKKjA9R";

    public static async Task SendAsync(string message)
    {
        var payload = new { content = message };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _http.PostAsync(webhookUrl, content);
    }
}

