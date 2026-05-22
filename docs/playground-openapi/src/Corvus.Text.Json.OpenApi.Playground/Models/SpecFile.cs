namespace Corvus.Text.Json.OpenApi.Playground.Models;

public class SpecFile
{
    public string Name { get; set; } = "openapi.json";
    public string Content { get; set; } = "{}";
    public bool IsRoot { get; set; } = true;
    public string CleanContent { get; set; } = "";
    public bool IsDirty => Content != CleanContent;
    public void MarkClean() => CleanContent = Content;
    public bool IsNew { get; set; }
}