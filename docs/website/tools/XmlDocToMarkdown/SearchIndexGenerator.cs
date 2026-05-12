using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace XmlDocToMarkdown;

/// <summary>
/// Generates a search-index.json file for Lunr-based search integration.
/// </summary>
public sealed class SearchIndexGenerator(string outputPath, string baseUrl)
{
    public void Generate(Dictionary<string, NamespaceInfo> namespaces)
    {
        List<SearchEntry> entries = [];

        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            string pageFileName = MarkdownGenerator.NamespaceToFileName(ns);
            string pageUrl = $"{baseUrl}/{pageFileName}.html";

            foreach (TypeInfo type in kvp.Value.Types)
            {
                AddTypeEntries(entries, type, ns, pageUrl);
            }
        }

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        string json = JsonSerializer.Serialize(entries, options);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"  Written: {outputPath} ({entries.Count} entries)");
    }

    private void AddTypeEntries(List<SearchEntry> entries, TypeInfo type, string ns, string pageUrl)
    {
        string nsSlug = MarkdownGenerator.NamespaceToFileName(ns);
        string typeSlug = MarkdownGenerator.TypeToSlug(type.Name);
        string typeUrl = $"{baseUrl}/{nsSlug}-{typeSlug}.html";

        // Build keywords from the type
        List<string> keywords = [type.Name, type.Kind, ns];
        keywords.AddRange(type.GenericParameters);

        // Build body from all member summaries
        StringBuilder body = new();
        if (!string.IsNullOrEmpty(type.Documentation?.Summary))
        {
            body.AppendLine(type.Documentation!.Summary);
        }

        if (!string.IsNullOrEmpty(type.Documentation?.Remarks))
        {
            body.AppendLine(type.Documentation!.Remarks);
        }

        foreach (MemberInfo prop in type.Properties)
        {
            if (!string.IsNullOrEmpty(prop.Documentation?.Summary))
            {
                body.AppendLine($"{prop.Name}: {prop.Documentation!.Summary}");
            }
        }

        foreach (MemberInfo method in type.Methods)
        {
            if (!string.IsNullOrEmpty(method.Documentation?.Summary))
            {
                body.AppendLine($"{method.Name}: {method.Documentation!.Summary}");
            }
        }

        entries.Add(new SearchEntry
        {
            Url = typeUrl,
            Title = type.Name,
            Description = type.Documentation?.Summary ?? string.Empty,
            Keywords = string.Join(" ", keywords),
            Body = body.ToString().Trim(),
        });

        // Add member page entries
        AddMemberEntries(entries, type, ns, nsSlug, typeSlug);

        // Add entries for each nested type
        foreach (TypeInfo nested in type.NestedTypes)
        {
            AddTypeEntries(entries, nested, ns, pageUrl);
        }
    }

    private void AddMemberEntries(List<SearchEntry> entries, TypeInfo type, string ns, string nsSlug, string typeSlug)
    {
        // Constructors
        if (type.Constructors.Count > 0)
        {
            string memberUrl = $"{baseUrl}/{MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, "ctor")}.html";
            entries.Add(new SearchEntry
            {
                Url = memberUrl,
                Title = $"{type.Name} Constructor",
                Description = type.Constructors[0].Documentation?.Summary ?? string.Empty,
                Keywords = $"{type.Name} constructor new {ns}",
                Body = string.Join(" ", type.Constructors.Select(c => c.Documentation?.Summary ?? "")),
            });
        }

        // Properties
        foreach (MemberInfo prop in type.Properties)
        {
            string memberUrl = $"{baseUrl}/{MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, MarkdownGenerator.MemberToSlug(prop.GroupKey))}.html";
            entries.Add(new SearchEntry
            {
                Url = memberUrl,
                Title = $"{type.Name}.{prop.Name}",
                Description = prop.Documentation?.Summary ?? string.Empty,
                Keywords = $"{type.Name} {prop.Name} property {ns}",
                Body = prop.Documentation?.Summary ?? "",
            });
        }

        // Methods (grouped by name)
        foreach (IGrouping<string, MemberInfo> group in type.Methods.GroupBy(m => m.GroupKey))
        {
            string memberUrl = $"{baseUrl}/{MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, MarkdownGenerator.MemberToSlug(group.Key))}.html";
            entries.Add(new SearchEntry
            {
                Url = memberUrl,
                Title = $"{type.Name}.{group.Key}",
                Description = group.First().Documentation?.Summary ?? string.Empty,
                Keywords = $"{type.Name} {group.Key} method {ns}",
                Body = string.Join(" ", group.Select(m => m.Documentation?.Summary ?? "")),
            });
        }

        // Operators (grouped by CLR name)
        foreach (IGrouping<string, MemberInfo> group in type.Operators.GroupBy(m => m.GroupKey))
        {
            string memberUrl = $"{baseUrl}/{MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, MarkdownGenerator.MemberToSlug(group.Key))}.html";
            string displayName = group.Key.Replace("op_", "");
            entries.Add(new SearchEntry
            {
                Url = memberUrl,
                Title = $"{type.Name} {displayName} Operator",
                Description = group.First().Documentation?.Summary ?? string.Empty,
                Keywords = $"{type.Name} {displayName} operator {ns}",
                Body = string.Join(" ", group.Select(m => m.Documentation?.Summary ?? "")),
            });
        }
    }

    private sealed class SearchEntry
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
