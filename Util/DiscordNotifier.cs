using Bot.GameEntity;
using System.Text;
using System.Text.Json;

namespace Bot.Util;

public static class DiscordNotifier
{
    private static readonly HttpClient _http = new();

    public static Task PlayerOnScreenAsync(IEnumerable<Creature> creatures, string url)
    {
        var names = creatures.Select(c => c.Name).ToArray(); // snapshot
        var message = "Player on Screen.\n" + string.Join("\n", names);

        //Discord max 2k char
        if (message.Length > 1900)
            message = message[..1900] + "\n...(truncated)";

        return SendAsync(message, url);
    }

    private static async Task SendAsync(string message, string url)
    {
        try
        {
            var payload = new { content = message };
            var json = JsonSerializer.Serialize(payload);

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send discord message: {ex}");
        }

    }
}

