namespace Corvus.Text.Json.OpenApi.TypeScript.Playground.Models;

public class GenerationResult
{
    public List<GeneratedFile> Files { get; set; } = [];
    public List<OperationNode> Operations { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public string? SpecVersion { get; set; }
}

public class GeneratedFile
{
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsClient { get; set; }
    public bool IsRequestResponse { get; set; }
    public bool IsModel { get; set; }
}