using Discord;
using Discord.WebSocket;

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

        await _client.LoginAsync(TokenType.Bot, "");
        await _client.StartAsync();

        _logger.LogInformation("起動完了");

        await Task.Delay(-1, stoppingToken);
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