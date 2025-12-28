using Discord;
using Discord.WebSocket;
using MCBot;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
	GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
}));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
