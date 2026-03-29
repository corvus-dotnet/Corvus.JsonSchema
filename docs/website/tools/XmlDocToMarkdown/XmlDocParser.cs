using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XmlDocToMarkdown;

/// <summary>
/// Represents a documented member parsed from the XML documentation file.
/// </summary>
public sealed class DocMember
{
    public string Name { get; set; } = string.Empty;
    public char MemberType { get; set; } // T, M, P, F, E
    public string Summary { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string Returns { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public List<DocParam> Params { get; set; } = [];
    public List<DocParam> TypeParams { get; set; } = [];
    public List<DocException> Exceptions { get; set; } = [];
    public List<string> SeeAlso { get; set; } = [];
}

public sealed class DocParam
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class DocException
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Parses an XML documentation file into a dictionary of <see cref="DocMember"/> keyed by member ID.
/// </summary>
public sealed partial class XmlDocParser(string xmlPath)
{
    /// <summary>
    /// When set, resolves type crefs to page URLs instead of anchor links.
    /// Key: full type name (e.g. "Corvus.Text.Json.JsonElement"), Value: page URL.
    /// </summary>
    public static Dictionary<string, string>? TypeUrlMap { get; set; }
    public Dictionary<string, DocMember> Parse()
    {
        Dictionary<string, DocMember> members = new(StringComparer.Ordinal);

        XDocument doc = XDocument.Load(xmlPath);
        XElement? membersElement = doc.Root?.Element("members");
        if (membersElement is null)
        {
            return members;
        }

        foreach (XElement memberElement in membersElement.Elements("member"))
        {
            string? nameAttr = memberElement.Attribute("name")?.Value;
            if (nameAttr is null || nameAttr.Length < 2 || nameAttr[1] != ':')
            {
                continue;
            }

            DocMember member = new()
            {
                Name = nameAttr[2..],
                MemberType = nameAttr[0],
                Summary = ConvertXmlToMarkdown(memberElement.Element("summary")),
                Remarks = ConvertXmlToMarkdown(memberElement.Element("remarks")),
                Returns = ConvertXmlToMarkdown(memberElement.Element("returns")),
                Value = ConvertXmlToMarkdown(memberElement.Element("value")),
                Example = ConvertXmlToMarkdown(memberElement.Element("example")),
            };

            foreach (XElement param in memberElement.Elements("param"))
            {
                member.Params.Add(new DocParam
                {
                    Name = param.Attribute("name")?.Value ?? string.Empty,
                    Description = ConvertXmlToMarkdown(param),
                });
            }

            foreach (XElement typeParam in memberElement.Elements("typeparam"))
            {
                member.TypeParams.Add(new DocParam
                {
                    Name = typeParam.Attribute("name")?.Value ?? string.Empty,
                    Description = ConvertXmlToMarkdown(typeParam),
                });
            }

            foreach (XElement exception in memberElement.Elements("exception"))
            {
                string? cref = exception.Attribute("cref")?.Value;
                member.Exceptions.Add(new DocException
                {
                    Type = cref is not null ? StripMemberPrefix(cref) : string.Empty,
                    Description = ConvertXmlToMarkdown(exception),
                });
            }

            foreach (XElement seeAlso in memberElement.Elements("seealso"))
            {
                string? cref = seeAlso.Attribute("cref")?.Value;
                if (cref is not null)
                {
                    member.SeeAlso.Add(StripMemberPrefix(cref));
                }
            }

            members[nameAttr] = member;
        }

        return members;
    }

    private static string ConvertXmlToMarkdown(XElement? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        ConvertNodes(sb, element.Nodes());
        return CollapseWhitespace().Replace(sb.ToString().Trim(), " ");
    }

    private static void ConvertNodes(StringBuilder sb, IEnumerable<XNode> nodes)
    {
        foreach (XNode node in nodes)
        {
            switch (node)
            {
                case XText text:
                    sb.Append(text.Value);
                    break;

                case XElement el:
                    ConvertElement(sb, el);
                    break;
            }
        }
    }

    private static void ConvertElement(StringBuilder sb, XElement el)
    {
        switch (el.Name.LocalName)
        {
            case "see":
                string? cref = el.Attribute("cref")?.Value;
                if (cref is not null)
                {
                    string stripped = StripMemberPrefix(cref);
                    char memberType = cref.Length > 1 && cref[1] == ':' ? cref[0] : 'T';
                    string? url = ResolveTypeUrl(stripped);

                    // Fall back to external documentation URLs
                    url ??= ResolveExternalTypeUrl(stripped);

                    if (memberType is 'M' or 'P' or 'F' or 'E')
                    {
                        // Member reference — display the member name and link to type#anchor
                        string memberName = GetMemberDisplayName(stripped);
                        if (url is not null)
                        {
                            string anchor = GetMemberAnchor(stripped, memberType);
                            sb.Append($"[`{memberName}`]({url}#{anchor})");
                        }
                        else
                        {
                            sb.Append($"`{memberName}`");
                        }
                    }
                    else
                    {
                        // Type reference — display the short type name
                        string typeName = GetShortTypeName(stripped);
                        if (url is not null)
                        {
                            sb.Append($"[`{typeName}`]({url})");
                        }
                        else
                        {
                            sb.Append($"`{typeName}`");
                        }
                    }
                }
                else
                {
                    string? langword = el.Attribute("langword")?.Value;
                    if (langword is not null)
                    {
                        sb.Append($"`{langword}`");
                    }
                }

                break;

            case "paramref":
                string? paramName = el.Attribute("name")?.Value;
                if (paramName is not null)
                {
                    sb.Append($"`{paramName}`");
                }

                break;

            case "typeparamref":
                string? typeParamName = el.Attribute("name")?.Value;
                if (typeParamName is not null)
                {
                    sb.Append($"`{typeParamName}`");
                }

                break;

            case "c":
                sb.Append('`');
                sb.Append(el.Value);
                sb.Append('`');
                break;

            case "code":
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(el.Value.Trim());
                sb.AppendLine("```");
                break;

            case "para":
                sb.AppendLine();
                sb.AppendLine();
                ConvertNodes(sb, el.Nodes());
                sb.AppendLine();
                break;

            case "list":
                sb.AppendLine();
                foreach (XElement item in el.Elements("item"))
                {
                    XElement? term = item.Element("term");
                    XElement? desc = item.Element("description");
                    if (term is not null)
                    {
                        sb.Append("- **");
                        ConvertNodes(sb, term.Nodes());
                        sb.Append("**");
                        if (desc is not null)
                        {
                            sb.Append(" — ");
                            ConvertNodes(sb, desc.Nodes());
                        }
                    }
                    else
                    {
                        sb.Append("- ");
                        ConvertNodes(sb, item.Nodes());
                    }

                    sb.AppendLine();
                }

                break;

            default:
                // For unknown elements, just include the inner content
                ConvertNodes(sb, el.Nodes());
                break;
        }
    }

    internal static string StripMemberPrefix(string cref)
    {
        if (cref.Length > 2 && cref[1] == ':')
        {
            return cref[2..];
        }

        return cref;
    }

    internal static string GetShortTypeName(string fullName)
    {
        // Handle generic arity markers like `1, `2
        string name = fullName;
        int parenIndex = name.IndexOf('(');
        if (parenIndex >= 0)
        {
            name = name[..parenIndex];
        }

        int lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
        {
            name = name[(lastDot + 1)..];
        }

        // Clean up generic arity markers for display
        int backtickIndex = name.IndexOf('`');
        if (backtickIndex >= 0)
        {
            name = name[..backtickIndex];
        }

        return name;
    }

    /// <summary>
    /// Resolves a full or partial type name to its API page URL, or null if not found.
    /// Handles both full names (Corvus.Text.Json.JsonElement) and member references
    /// (Corvus.Text.Json.JsonElement.Parse) by walking up the name hierarchy.
    /// </summary>
    internal static string? ResolveTypeUrl(string fullName)
    {
        if (TypeUrlMap is null || TypeUrlMap.Count == 0)
            return null;

        // Strip method parameters: Corvus.Text.Json.JsonElement.Parse(System.String) → Corvus.Text.Json.JsonElement.Parse
        string name = fullName;
        int parenIdx = name.IndexOf('(');
        if (parenIdx >= 0)
            name = name[..parenIdx];

        // Strip generic arity: Corvus.Text.Json.JsonDocumentBuilder`1 → Corvus.Text.Json.JsonDocumentBuilder
        int backtickIdx = name.IndexOf('`');
        if (backtickIdx >= 0)
            name = name[..backtickIdx];

        // Try exact match first, then walk up dots to find the enclosing type
        while (name.Contains('.'))
        {
            if (TypeUrlMap.TryGetValue(name, out string? url))
                return url;

            // Also try with generic arity variants
            foreach (int arity in new[] { 1, 2, 3, 4 })
            {
                if (TypeUrlMap.TryGetValue($"{name}`{arity}", out url))
                    return url;
            }

            // Walk up: Corvus.Text.Json.JsonElement.Parse → Corvus.Text.Json.JsonElement
            int lastDot = name.LastIndexOf('.');
            name = name[..lastDot];
        }

        return null;
    }

    internal static string GetTypeAnchor(string fullName)
    {
        string shortName = GetShortTypeName(fullName);
        return shortName.Replace('.', '-');
    }

    /// <summary>
    /// Extracts the member name from a full cref like "Corvus.Text.Json.JsonElement.Parse(System.String)".
    /// Returns "Parse" — the last segment before any parentheses.
    /// </summary>
    internal static string GetMemberDisplayName(string fullName)
    {
        string name = fullName;

        // Strip parameters
        int parenIdx = name.IndexOf('(');
        if (parenIdx >= 0)
            name = name[..parenIdx];

        // Strip generic arity
        int backtickIdx = name.IndexOf('`');
        if (backtickIdx >= 0)
            name = name[..backtickIdx];

        // Take the last segment (the member name)
        int lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name[(lastDot + 1)..];

        // Handle constructor references: #ctor → the type name
        if (name == "#ctor")
        {
            string withoutMember = fullName;
            int ctorDot = withoutMember.LastIndexOf(".#ctor", StringComparison.Ordinal);
            if (ctorDot >= 0)
            {
                withoutMember = withoutMember[..ctorDot];
                int lastTypeDot = withoutMember.LastIndexOf('.');
                if (lastTypeDot >= 0)
                    return withoutMember[(lastTypeDot + 1)..];
                return withoutMember;
            }
        }

        return name;
    }

    /// <summary>
    /// Generates a markdown-compatible anchor ID for a member.
    /// Must match the anchors that Markdig generates from heading text like "### Parse `static`".
    /// Pattern: lowercase member name, with modifiers stripped (Markdig adds -static, -virtual, etc.).
    /// We use just the lowercase name since the exact heading anchor depends on duplicates.
    /// </summary>
    internal static string GetMemberAnchor(string fullName, char memberType)
    {
        string memberName = GetMemberDisplayName(fullName);

        // Lowercase and replace non-alphanumeric with hyphens (matching Markdig's auto-ID)
        return memberName.ToLowerInvariant()
            .Replace('<', '-')
            .Replace(">", "")
            .Replace(' ', '-')
            .TrimEnd('-');
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespace();

    /// <summary>
    /// Returns an external documentation URL for well-known type prefixes
    /// (System.*, Microsoft.*, NodaTime.*), or <c>null</c> if not recognised.
    /// </summary>
    internal static string? ResolveExternalTypeUrl(string fullName)
    {
        // Strip method parameters and generic arity for URL resolution
        int parenIdx = fullName.IndexOf('(');
        string name = parenIdx >= 0 ? fullName[..parenIdx] : fullName;

        if (name.StartsWith("System.", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.", StringComparison.Ordinal))
        {
            string urlName = name.ToLowerInvariant().Replace('`', '-');
            return $"https://learn.microsoft.com/dotnet/api/{urlName}";
        }

        if (name.StartsWith("NodaTime.", StringComparison.Ordinal))
        {
            int backtick = name.IndexOf('`');
            string cleanName = backtick >= 0 ? name[..backtick] : name;
            return $"https://www.nodatime.org/3.3.x/api/{cleanName}.html";
        }

        return null;
    }
}
