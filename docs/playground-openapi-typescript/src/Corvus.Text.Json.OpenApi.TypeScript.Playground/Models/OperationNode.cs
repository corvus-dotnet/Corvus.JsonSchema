namespace Corvus.Text.Json.OpenApi.TypeScript.Playground.Models;

/// <summary>
/// A node in the operation tree. Can be a group (tag or path) or a leaf (operation).
/// </summary>
public class OperationNode
{
    public string Path { get; set; } = "";
    public string? Method { get; set; }
    public string? OperationId { get; set; }
    public string? Summary { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsChecked { get; set; } = true;
    public bool IsExpanded { get; set; } = true;
    public List<OperationNode> Children { get; set; } = [];
    public bool IsGroup => Method is null;

    /// <summary>
    /// Gets the group name for display (tag name or path).
    /// </summary>
    public string GroupName { get; set; } = "";
}