using Discord;
using Discord.WebSocket;
using System.Text.Json;
using CoreRCON;
using System.Net;

namespace MCBot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DiscordSocketClient _client;
    private RCON _rcon;
    private const string RCON_PASSWORD = "pass";
    private const string SERVER_IP = "127.17.0.1";
    private const ushort RCON_PORT = 25575;


    public Worker(ILogger<Worker> logger, DiscordSocketClient client)
    {
        _logger = logger;
        _client = client;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += LogAsync;
        _client.MessageReceived += OnMessageReceived;

        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(SERVER_IP),RCON_PORT);
            _rcon = new RCON(endpoint, RCON_PASSWORD);

            await _rcon.ConnectAsync();
            _logger.LogInformation("RCON接続成功");
        }
        catch
        {
            _logger.LogInformation("RCON接続失敗");
        }

        var jsonPath = Path.Combine(AppContext.BaseDirectory, "token.json");

        using var fs = File.OpenRead(jsonPath);
        var cfg = await JsonSerializer.DeserializeAsync<TokenConfig>(fs);
        var token = cfg?.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("トークンが空です。");
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

        if(message.Content.StartsWith("!say "))
        {
            string text = message.Content.Replace("!say ","");

            string result = await _rcon.SendCommandAsync($"say {text}");

            await message.Channel.SendMessageAsync($"送信しました: {result}");
        }
    }
}