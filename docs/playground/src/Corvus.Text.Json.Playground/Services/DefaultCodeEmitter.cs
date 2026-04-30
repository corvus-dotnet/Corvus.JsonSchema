using System.Text;
using Corvus.Text.Json.Playground.Models;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Generates default user code based on the generated type map.
/// This gives users a starting point for experimenting with the generated types.
/// </summary>
public static class DefaultCodeEmitter
{
    /// <summary>
    /// Emit default C# code that uses the root generated type.
    /// </summary>
    public static string Emit(IReadOnlyList<TypeMapEntry> typeMap)
    {
        if (typeMap.Count == 0)
        {
            return "// No types were generated. Check the schema for errors.\n";
        }

        TypeMapEntry rootType = typeMap[0];
        var sb = new StringBuilder();

        sb.AppendLine();

        // Build a sample JSON instance from the type map
        string sampleJson = BuildSampleJson(rootType, typeMap);

        sb.AppendLine($"// Parse a JSON instance as a {rootType.TypeName}");
        sb.AppendLine("string json = \"\"\"");
        sb.AppendLine($"    {sampleJson}");
        sb.AppendLine("    \"\"\";");
        sb.AppendLine();
        sb.AppendLine($"using var parsedDoc = ParsedJsonDocument<{rootType.FullTypeName}>.Parse(json);");
        sb.AppendLine($"{rootType.FullTypeName} value = parsedDoc.RootElement;");
        sb.AppendLine();
        sb.AppendLine("// Print the value");
        sb.AppendLine("Console.WriteLine(value.ToString());");

        return sb.ToString();
    }

    private static string BuildSampleJson(
        TypeMapEntry type,
        IReadOnlyList<TypeMapEntry> allTypes,
        HashSet<string>? visited = null)
    {
        if (type.Kind != "object" || type.Properties.Count == 0)
        {
            return "{}";
        }

        visited ??= [];
        if (!visited.Add(type.FullTypeName))
        {
            // Circular reference — emit empty object to break the cycle
            return "{}";
        }

        var parts = new List<string>();
        foreach (TypeMapProperty prop in type.Properties)
        {
            string sampleValue = GetSampleValue(prop, allTypes, visited);
            parts.Add($"\"{prop.Name}\": {sampleValue}");
        }

        visited.Remove(type.FullTypeName);
        return "{ " + string.Join(", ", parts) + " }";
    }

    private static string GetSampleValue(
        TypeMapProperty prop,
        IReadOnlyList<TypeMapEntry> allTypes,
        HashSet<string>? visited = null)
    {
        // Check if property type is a known complex type in the map
        TypeMapEntry? complexType = allTypes
            .FirstOrDefault(t => t.TypeName == prop.TypeName || t.FullTypeName == prop.TypeName);

        if (complexType is not null && complexType.Kind == "object")
        {
            return BuildSampleJson(complexType, allTypes, visited);
        }

        return prop.TypeName.ToLowerInvariant() switch
        {
            "jsonstring" or "string" => "\"example\"",
            "jsoninteger" or "jsonint32" or "jsonint64" or "integer" => "0",
            "jsonnumber" or "jsondouble" or "jsonsingle" or "number" => "0.0",
            "jsonboolean" or "boolean" => "true",
            _ => "null",
        };
    }

}
