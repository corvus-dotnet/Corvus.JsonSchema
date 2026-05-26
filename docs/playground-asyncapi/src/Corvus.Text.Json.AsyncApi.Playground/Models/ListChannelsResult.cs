namespace Corvus.Text.Json.AsyncApi.Playground.Models;

public class ListChannelsResult
{
    public List<ChannelNode> Channels { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public string? SpecVersion { get; set; }
}