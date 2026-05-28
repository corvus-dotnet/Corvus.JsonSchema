namespace Corvus.Text.Json.AsyncApi.Playground.Models;

public class GenerationResult
{
    public List<GeneratedFile> Files { get; set; } = [];
    public List<ChannelNode> Channels { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public string? SpecVersion { get; set; }
}

public class GeneratedFile
{
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsHandler { get; set; }
    public bool IsProducer { get; set; }
    public bool IsModel { get; set; }
}