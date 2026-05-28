namespace Corvus.Text.Json.AsyncApi.Playground.Models;

/// <summary>
/// A node in the channel tree. Can be a group (channel) or a leaf (operation).
/// </summary>
public class ChannelNode
{
    public string Channel { get; set; } = "";
    public string? Action { get; set; }
    public string? OperationId { get; set; }
    public string? Summary { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsChecked { get; set; } = true;
    public bool IsExpanded { get; set; } = true;
    public List<ChannelNode> Children { get; set; } = [];
    public bool IsGroup => Action is null;
    public string GroupName { get; set; } = "";
}