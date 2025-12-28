using Discord;
using Discord.WebSocket;
using System.Text.Json;
using System.IO;

namespace MCBot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DiscordSocketClient _client;

    public Worker(ILogger<Worker> logger, DiscordSocketClient client)
    {
        _logger = logger;
        _client = client;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += LogAsync;
        _client.MessageReceived += OnMessageReceived;

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "token.json");

        using var fs = File.OpenRead(jsonPath);
        var cfg = await JsonSerializer.DeserializeAsync<TokenConfig>(fs);
        var token = cfg?.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("token.json の Token が空です。");
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _logger.LogInformation("起動完了");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private sealed class TokenConfig
    {
        public string? Token { get; set; }
    }

    private Task LogAsync(LogMessage msg)
    {
        _logger.LogInformation(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if(message.Author.IsBot) return;

        if(message.Content == "こんにちは")
        {
            await message.Channel.SendMessageAsync("こんにちは、世界。");
        }
    }
}