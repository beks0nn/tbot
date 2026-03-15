using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bot.Chat;

public sealed class AiChatService : IDisposable
{
    private readonly HttpClient _http = new();
    private const string OllamaUrl = "http://localhost:11434/api/chat";
    private const string Model = "llama3.1:8b";

    public async Task<string?> GenerateResponseAsync(
        string systemPrompt,
        List<ConversationTurn> conversation,
        CancellationToken ct)
    {
        if (conversation.Count == 0)
            return null;

        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });

        foreach (var turn in conversation)
            messages.Add(new { role = turn.Role == "user" ? "user" : "assistant", content = turn.Content });

        var body = new
        {
            model = Model,
            messages,
            stream = false,
            options = new { num_predict = 30 }
        };

        try
        {
            var response = await _http.PostAsJsonAsync(OllamaUrl, body, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"[AiChat] Ollama error: {response.StatusCode} - {errorBody}");
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
            var text = json?.Message?.Content;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AiChat] Error: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

public sealed record ConversationTurn(string Role, string Content);
