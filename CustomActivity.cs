
using Discord;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace MCBot;

public class CustomActivity : IActivity
{
    public string Name { get; set; } = "Minecraft";
    public ActivityType Type { get; set; } = ActivityType.Playing;
    public ActivityProperties Flags { get; set; } = ActivityProperties.None;
    public string Details { get; set; } = "";

    [JsonProperty("state")]
    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonProperty("timestamps")]
    [JsonPropertyName("timestamps")]
    public CustomTimestamps? Timestamps { get; set; }

    [JsonProperty("assets")]
    [JsonPropertyName("assets")]
    public CustomAssets? Assets { get; set; }

    [JsonProperty("application_id")]
    [JsonPropertyName("application_id")]
    public ulong? ApplicationId { get; set; }
}

public class CustomTimestamps
{
    [JsonProperty("start")]
    [JsonPropertyName("start")]
    public long? Start { get; set; }

    [JsonProperty("end")]
    [JsonPropertyName("end")]
    public long? End { get; set; }
}

public class CustomAssets
{
    [JsonProperty("large_image")]
    [JsonPropertyName("large_image")]
    public string? LargeImage { get; set; }

    [JsonProperty("large_text")]
    [JsonPropertyName("large_text")]
    public string? LargeText { get; set; }

    [JsonProperty("small_image")]
    [JsonPropertyName("small_image")]
    public string? SmallImage { get; set; }

    [JsonProperty("small_text")]
    [JsonPropertyName("small_text")]
    public string? SmallText { get; set; }
}
