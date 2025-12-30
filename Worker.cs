using Discord;
using Discord.WebSocket;
using System.Text.Json;
using CoreRCON;
using System.Net;
using System.Text;
using System.Data;

namespace MCBot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DiscordSocketClient _client;
    private readonly IConfiguration _config;
    private RCON _rcon;
    private IPEndPoint _rconEndpoint;
    private string _rconPassword;
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
        
        _rconEndpoint = new IPEndPoint(IPAddress.Parse(rconIp), rconPort);
        _rconPassword = rconPass;
        _rcon = new RCON(_rconEndpoint, _rconPassword);

        // Discordボットを先に起動
        await _client.LoginAsync(TokenType.Bot, discordToken);
        await _client.StartAsync();
        _logger.LogInformation("Discord起動開始");

        // Discordボットの準備完了を待つ
        var readyTask = new TaskCompletionSource<bool>();
        Task ReadyHandler()
        {
            readyTask.SetResult(true);
            return Task.CompletedTask;
        }
        _client.Ready += ReadyHandler;
        await readyTask.Task;
        _logger.LogInformation("Discord準備完了");

        // RCON接続に成功するまで3秒毎にリトライ
        bool rconConnected = false;
        while (!rconConnected && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _rcon.ConnectAsync();
                _logger.LogInformation("RCON接続成功");
                rconConnected = true;
                
                // RCON接続成功時にチャンネルにメッセージ送信
                var channel = _client.GetChannel(channelId) as IMessageChannel;
                if (channel != null)
                {
                    await channel.SendMessageAsync("## Server Started");
                    _logger.LogInformation("Server Startedメッセージ送信完了");
                }
                else
                {
                    _logger.LogWarning("チャンネルが見つかりませんでした");
                }
                await _client.SetActivityAsync(new Game("Minecraft Server", ActivityType.Playing));
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"RCON接続失敗: {ex.Message}. 3秒後に再試行します...");
                await Task.Delay(3000, stoppingToken);
            }
        }

        // var jsonPath = Path.Combine(AppContext.BaseDirectory, "token.json");

        // using var fs = File.OpenRead(jsonPath);
        // var cfg = await JsonSerializer.DeserializeAsync<TokenConfig>(fs);
        // var token = cfg?.Token;
        // if (string.IsNullOrWhiteSpace(token))
        // {
        //     throw new InvalidOperationException("トークンが空です。");
        // }

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

        _logger?.LogInformation("ログ監視開始");

        while (!token.IsCancellationRequested)
        {
            try
            {
                // ログファイルが存在しない場合は待機
                while (!File.Exists(logPath) && !token.IsCancellationRequested)
                {
                    await Task.Delay(3000, token);
                    _logger?.LogInformation("ログファイルを再度探しています...");
                }

                if (token.IsCancellationRequested) break;

                using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.Write))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.End);

                    while (!token.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();

                        if (line != null)
                        {
                            await ProcessLogLine(line, channelId);
                        }
                        else
                        {
                            await Task.Delay(500, token);

                            // RCON接続状態をチェック（定期的に）
                            if (_rcon != null && _logger != null)
                            {
                                try
                                {
                                    // RCONが接続しているかテスト
                                    await _rcon.SendCommandAsync("list");
                                }
                                catch (Exception)
                                {
                                    // 接続が切れている場合、再接続を試みる
                                    _logger.LogWarning("RCON接続が切れています。再接続を試みています...");
                                    if (_rconEndpoint != null && _rconPassword != null)
                                    {
                                        try
                                        {
                                            _rcon = new RCON(_rconEndpoint, _rconPassword);
                                            await _rcon.ConnectAsync();
                                            _logger.LogInformation("RCON再接続成功");
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning($"RCON再接続失敗: {ex.Message}");
                                        }
                                    }
                                }
                            }

                            // ログファイルが削除されていないかチェック
                            if (!File.Exists(logPath))
                            {
                                _logger?.LogWarning("ログファイルが削除されました。再度探します...");
                                break; // 内側のwhile ループを抜けて、ファイルを探し直す
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"ログ監視中にエラーが発生しました: {ex.Message}");
                await Task.Delay(3000, token); // エラー後に3秒待機して再試行
            }
        }
    }

    private async Task ProcessLogLine(string line, ulong channelId)
    {
        if (line == null || _client == null || _logger == null) return;

        var channel = _client.GetChannel(channelId) as IMessageChannel;

        if(line.Contains("RCON Client")) return;
        if(!line.Contains("[Server thread/INFO]")) return;
        if(line.Contains("[Rcon]")) return;

        if(line.Contains("Stopping server")) 
        {
            if (channel != null)
            {
                await channel.SendMessageAsync("## Server Stopped");
            }
            await _client.SetActivityAsync(null);
        }
        if(line.Contains("]: Done ("))
        {
            if (channel != null)
            {
                await channel.SendMessageAsync("## Sever Started");
            }
            await _client.SetActivityAsync(new Game("Minecraft Server", ActivityType.Playing));
            
            // サーバー起動時にRCON再接続を試みる
            if (_rcon != null && _rconEndpoint != null && _rconPassword != null)
            {
                try
                {
                    _logger.LogInformation("サーバー起動検出 - RCON再接続を試みています...");
                    _rcon = new RCON(_rconEndpoint, _rconPassword);
                    await _rcon.ConnectAsync();
                    _logger.LogInformation("RCON再接続成功");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"RCON再接続失敗: {ex.Message}");
                }
            }
        }


        List<string> Conditions = new List<string>{": <","joined the game","left the game","has made the advancement","has completed the challenge",": [@",": [",};
        bool shouldSend = false;

        foreach(string text in Conditions)
        {
            if(line.Contains(text)) shouldSend = true;
        }

        if (line.Contains("[Rcon]")) shouldSend = false;

        if (shouldSend)
        {
            if (channel != null)
            {
                string messageToSend = line;
                int splitIndex = line.IndexOf("]: ");
                if(line.Contains("[Rcon]"))
                {
                    splitIndex = line.IndexOf("[Rcon] ");
                    messageToSend = line.Substring(splitIndex + 6);
                }
                else if (splitIndex != -1)
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
        if (_config == null) return;
        if(message.Channel.Id != _config.GetValue<ulong>("Discord:ChannelId")) return;
        
        // if(message.Content.StartsWith("消えてなくなってしまえぇぇぇ"))
        // {
        //     await SendRconCommandSafeAsync($"kill @e");
        // }

        if(message.Content.StartsWith("./"))
        {
            string commandtext = message.Content.Substring(2);
            await SendRconCommandSafeAsync(commandtext);
            return;
        }

        await SendRconCommandSafeAsync($"say {message.Author.Username} {message.Content}");
    }

    private async Task SendRconCommandSafeAsync(string command)
    {
        if (_rcon == null || _logger == null || _rconEndpoint == null || _rconPassword == null)
        {
            _logger?.LogWarning("RCON が初期化されていません");
            return;
        }

        int maxRetries = 3;
        int currentRetry = 0;

        while (currentRetry < maxRetries)
        {
            try
            {
                await _rcon.SendCommandAsync(command);
                return; // 成功したら終了
            }
            catch (Exception ex)
            {
                currentRetry++;
                _logger.LogWarning($"RCON送信失敗 (試行 {currentRetry}/{maxRetries}): {ex.Message}");

                if (currentRetry < maxRetries)
                {
                    // 接続をリセットして再接続を試みる
                    try
                    {
                        _logger.LogInformation("RCON再接続を試みています...");
                        _rcon = new RCON(_rconEndpoint, _rconPassword);
                        await _rcon.ConnectAsync();
                        _logger.LogInformation("RCON再接続成功");
                        await Task.Delay(500); // 接続安定化待機
                    }
                    catch (Exception reconnectEx)
                    {
                        _logger.LogWarning($"RCON再接続失敗: {reconnectEx.Message}");
                        await Task.Delay(1000); // 次の試行まで待機
                    }
                }
            }
        }

        _logger.LogError($"RCON送信完全失敗: {command}");
    }

}