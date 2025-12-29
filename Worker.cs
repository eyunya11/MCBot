using Discord;
using Discord.WebSocket;
using System.Text.Json;
using CoreRCON;
using System.Net;
using System.Text;

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

        string logPath = _config["Minecraft:LogPath"];
        ulong channelId = _config.GetValue<ulong>("Discord:ChannelId");
        
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

        _ = Task.Run(() => WatchLogAsync(logPath, channelId, stoppingToken), stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task WatchLogAsync(string logPath, ulong channelId, CancellationToken token)
    {
        while (!File.Exists(logPath) && !token.IsCancellationRequested)
        {
            await Task.Delay(3000, token);
            _logger.LogInformation("ログファイルが見つかりませんでした");
        }

        _logger.LogInformation("ログ監視開始");

        using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.Write))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            reader.BaseStream.Seek(0, SeekOrigin.End);

            while(!token.IsCancellationRequested)
            {
                string line = await reader.ReadLineAsync();

                if(line != null)
                {
                    await ProcessLogLine(line, channelId);
                }
                else
                {
                    await Task.Delay(500, token);
                }
            }
        }
    }

    private async Task ProcessLogLine(string line, ulong channelId)
    {
        if(line.Contains("RCON Client")) return;
        if(!line.Contains("[Server thread/INFO]")) return;

        bool shouldSend = false;

        if (line.Contains(": <")) shouldSend = true;
        else if (line.Contains("joined the game")) shouldSend = true;
        else if (line.Contains("left the game")) shouldSend = true;
        else if (line.Contains("has made the advancement")) shouldSend = true;
        else if (line.Contains("has completed the challenge")) shouldSend = true;
        else if (line.Contains(": [@")) shouldSend = false;
        else if (line.Contains(": [")) shouldSend = true;

        if (shouldSend)
        {
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel != null)
            {
                string messageToSend = line;
                int splitIndex = line.IndexOf("]: ");
                if (splitIndex != -1)
                {
                    messageToSend = line.Substring(splitIndex + 3);
                }

                await channel.SendMessageAsync(messageToSend);
            }
        }
    }

    private Task LogAsync(LogMessage msg)
    {
        _logger.LogInformation(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if(message.Author.IsBot) return;

        await _rcon.SendCommandAsync($"{message.Author.GlobalName} :{message.Content}");
    }
}