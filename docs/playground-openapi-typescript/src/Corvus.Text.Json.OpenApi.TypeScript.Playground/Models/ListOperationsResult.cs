namespace Corvus.Text.Json.OpenApi.TypeScript.Playground.Models;

public class ListOperationsResult
{
    public List<OperationNode> Operations { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public string? SpecVersion { get; set; }
}