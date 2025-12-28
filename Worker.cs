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
    private readonly IConfiguration _config;
    private RCON _rcon;

    public Worker(ILogger<Worker> logger, DiscordSocketClient client, IConfiguration config)
    {
        _logger = logger;
        _client = client;
        _config = config;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += LogAsync;
        _client.MessageReceived += OnMessageReceived;

        string rconIp = _config["Minecraft:IP"];
        string rconPass = _config["Minecraft:RconPassword"];
        ushort rconPort = _config.GetValue<ushort>("Minecraft:Port");
        string discordToken = _config["Discord:Token"];
        
        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(rconIp),rconPort);
            _rcon = new RCON(endpoint, rconPass);

            await _rcon.ConnectAsync();
            _logger.LogInformation("RCON接続成功");
        }
        catch
        {
            _logger.LogInformation("RCON接続失敗");
        }

        // var jsonPath = Path.Combine(AppContext.BaseDirectory, "token.json");

        // using var fs = File.OpenRead(jsonPath);
        // var cfg = await JsonSerializer.DeserializeAsync<TokenConfig>(fs);
        // var token = cfg?.Token;
        // if (string.IsNullOrWhiteSpace(token))
        // {
        //     throw new InvalidOperationException("トークンが空です。");
        // }

        await _client.LoginAsync(TokenType.Bot, discordToken);
        await _client.StartAsync();

        _logger.LogInformation("起動完了");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    // private sealed class TokenConfig
    // {
    //     public string? Token { get; set; }
    // }

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