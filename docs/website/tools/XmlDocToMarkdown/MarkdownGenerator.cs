using System.Text;

namespace XmlDocToMarkdown;

/// <summary>
/// Generates one Vellum markdown file per namespace containing all public types.
/// </summary>
public sealed class MarkdownGenerator(string outputDir, string baseUrl, string? namespaceDescriptionsDir = null, SourceLinkResolver? sourceResolver = null, string? typeExamplesDir = null)
{
    private const string FrontmatterDate = "2026-03-15T00:00:00.0+00:00";

    public void Generate(Dictionary<string, NamespaceInfo> namespaces)
    {
        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            NamespaceInfo nsInfo = kvp.Value;
            string fileName = NamespaceToFileName(ns) + ".md";
            string filePath = Path.Combine(outputDir, fileName);

            StringBuilder sb = new();
            WriteFrontmatter(sb, $"{ns} Namespace");
            WriteNamespaceContent(sb, nsInfo);

            File.WriteAllText(filePath, sb.ToString());
        }
    }

    private static void WriteFrontmatter(StringBuilder sb, string title)
    {
        sb.AppendLine("---");
        sb.AppendLine("ContentType: \"application/vnd.endjin.ssg.content+md\"");
        sb.AppendLine("PublicationStatus: Published");
        sb.AppendLine($"Date: {FrontmatterDate}");
        sb.AppendLine($"Title: \"{EscapeYamlString(title)}\"");
        sb.AppendLine("---");
    }

    /// <summary>
    /// Generates one markdown file per type, placed flat in the output directory.
    /// File names use the pattern: {nsSlug}-{typeSlug}.md
    /// </summary>
    public void GeneratePerType(Dictionary<string, NamespaceInfo> namespaces)
    {
        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            string nsSlug = NamespaceToFileName(ns);

            foreach (TypeInfo type in kvp.Value.Types)
            {
                string typeSlug = TypeToSlug(type.Name);
                string fileName = $"{nsSlug}-{typeSlug}.md";
                string filePath = Path.Combine(outputDir, fileName);

                string? typeExample = LoadExampleMarkdown($"{nsSlug}-{typeSlug}");

                StringBuilder sb = new();
                WriteFrontmatter(sb, $"{type.Name} \u2014 {ns}");
                WriteTypeBody(sb, type, ns, baseUrl, nsSlug, typeSlug, sourceResolver?.GetTypeSourceUrl(type.FullName), typeExample);

                File.WriteAllText(filePath, sb.ToString());
            }
        }
    }

    /// <summary>
    /// Generates one markdown file per member group (method overloads, property, etc.).
    /// File names use: {nsSlug}-{typeSlug}.{memberSlug}.md
    /// </summary>
    public int GenerateMemberPages(Dictionary<string, NamespaceInfo> namespaces)
    {
        int fileCount = 0;
        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            string nsSlug = NamespaceToFileName(ns);

            foreach (TypeInfo type in kvp.Value.Types)
            {
                string typeSlug = TypeToSlug(type.Name);
                fileCount += GenerateMemberPagesForType(ns, nsSlug, type, typeSlug);
            }
        }

        return fileCount;
    }

    private int GenerateMemberPagesForType(string ns, string nsSlug, TypeInfo type, string typeSlug)
    {
        int count = 0;
        // Constructors (all on one page)
        if (type.Constructors.Count > 0)
        {
            WriteMemberPageFile(ns, nsSlug, type, typeSlug, ".ctor", "ctor",
                $"{type.Name} Constructors", "Constructor", type.Constructors);
            count++;
        }

        // Properties (grouped by name — indexer overloads share one page)
        foreach (IGrouping<string, MemberInfo> group in type.Properties.GroupBy(p => p.GroupKey))
        {
            WriteMemberPageFile(ns, nsSlug, type, typeSlug, group.Key, MemberToSlug(group.Key),
                $"{type.Name}.{group.Key} Property", "Property", group.ToList());
            count++;
        }

        // Methods (grouped by name — all overloads on one page)
        foreach (IGrouping<string, MemberInfo> group in type.Methods.GroupBy(m => m.GroupKey))
        {
            WriteMemberPageFile(ns, nsSlug, type, typeSlug, group.Key, MemberToSlug(group.Key),
                $"{type.Name}.{group.Key} Method", "Method", group.ToList());
            count++;
        }

        // Operators (grouped by CLR name — e.g. all op_Implicit on one page)
        foreach (IGrouping<string, MemberInfo> group in type.Operators.GroupBy(m => m.GroupKey))
        {
            string displayGroupName = GetOperatorGroupDisplayName(group.Key);
            WriteMemberPageFile(ns, nsSlug, type, typeSlug, displayGroupName, MemberToSlug(group.Key),
                $"{type.Name}.{displayGroupName} Operator", "Operator", group.ToList());
            count++;
        }

        // Fields (each on its own page)
        foreach (MemberInfo field in type.Fields)
        {
            WriteMemberPageFile(ns, nsSlug, type, typeSlug, field.Name, MemberToSlug(field.GroupKey),
                $"{type.Name}.{field.Name} Field", "Field", [field]);
            count++;
        }

        // Events (each on its own page)
        foreach (MemberInfo evt in type.Events)
        {
            WriteMemberPageFile(ns, nsSlug, type, typeSlug, evt.Name, MemberToSlug(evt.GroupKey),
                $"{type.Name}.{evt.Name} Event", "Event", [evt]);
            count++;
        }

        return count;
    }

    private void WriteMemberPageFile(
        string ns, string nsSlug, TypeInfo type, string typeSlug,
        string memberDisplayName, string memberSlug,
        string pageTitle, string memberCategory,
        List<MemberInfo> members)
    {
        string fileBase = GetMemberPageFileBase(nsSlug, typeSlug, memberSlug);
        string filePath = Path.Combine(outputDir, fileBase + ".md");

        string? memberExample = LoadExampleMarkdown(fileBase);

        StringBuilder sb = new();
        WriteFrontmatter(sb, $"{pageTitle} \u2014 {ns}");
        WriteMemberPageBody(sb, ns, nsSlug, type, typeSlug, memberDisplayName, memberCategory, members, sourceResolver, memberExample);

        File.WriteAllText(filePath, sb.ToString());
    }

    private void WriteNamespaceContent(StringBuilder sb, NamespaceInfo nsInfo)
    {
        string nsSlug = NamespaceToFileName(nsInfo.Name);

        if (namespaceDescriptionsDir is not null)
        {
            string descPath = Path.Combine(namespaceDescriptionsDir, nsInfo.Name + ".md");
            if (File.Exists(descPath))
            {
                sb.Append(File.ReadAllText(descPath).TrimEnd());
                sb.AppendLine();
                sb.AppendLine();
            }
        }

        sb.AppendLine("| Type | Kind | Description |");
        sb.AppendLine("|------|------|-------------|");
        foreach (TypeInfo type in nsInfo.Types)
        {
            string summary = TruncateSummary(type.Documentation?.Summary ?? string.Empty);
            string typeSlug = TypeToSlug(type.Name);
            string typeUrl = $"{baseUrl}/{nsSlug}-{typeSlug}.html";
            sb.AppendLine($"| [{type.Name}]({typeUrl}) | {type.Kind} | {summary} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Loads a hand-authored example markdown file for a type or member.
    /// Files are named {slug}.md in the typeExamplesDir directory.
    /// </summary>
    private string? LoadExampleMarkdown(string slug)
    {
        if (typeExamplesDir is null)
        {
            return null;
        }

        string examplePath = Path.Combine(typeExamplesDir, slug + ".md");
        if (File.Exists(examplePath))
        {
            return File.ReadAllText(examplePath).TrimEnd();
        }

        return null;
    }

    /// <summary>
    /// Writes the body of a type summary page — declaration, summary, inheritance,
    /// and summary tables for each member category linking to dedicated member pages.
    /// </summary>
    internal static void WriteTypeBody(StringBuilder sb, TypeInfo type, string ns, string baseUrl, string? nsSlug = null, string? typeSlug = null, string? sourceUrl = null, string? typeExampleMarkdown = null)
    {
        // Definition metadata — Namespace, Assembly, Source grouped together
        sb.AppendLine("## Definition");
        sb.AppendLine();
        sb.AppendLine($"**Namespace:** {ns}  ");
        sb.AppendLine($"**Assembly:** Corvus.Text.Json.dll  ");
        if (sourceUrl is not null)
        {
            string displayPath = GetSourceDisplayPath(sourceUrl);
            sb.AppendLine($"**Source:** [{displayPath}]({sourceUrl})");
        }

        sb.AppendLine();

        if (!string.IsNullOrEmpty(type.Documentation?.Summary))
        {
            sb.AppendLine(type.Documentation!.Summary);
            sb.AppendLine();
        }

        sb.AppendLine("```csharp");
        sb.AppendLine(BuildTypeDeclaration(type));
        sb.AppendLine("```");
        sb.AppendLine();

        if (type.GenericParameters.Count > 0 && type.Documentation?.TypeParams.Count > 0)
        {
            sb.AppendLine("## Type Parameters");
            sb.AppendLine();
            sb.AppendLine("| Parameter | Description |");
            sb.AppendLine("|-----------|-------------|");
            foreach (DocParam tp in type.Documentation!.TypeParams)
            {
                sb.AppendLine($"| `{tp.Name}` | {EscapeDescriptionForTable(tp.Description)} |");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(type.Documentation?.Remarks))
        {
            sb.AppendLine("## Remarks");
            sb.AppendLine();
            sb.AppendLine(type.Documentation!.Remarks);
            sb.AppendLine();
        }

        // Inheritance chain (for classes with a base type)
        if (type.BaseType is not null && type.Kind == "class")
        {
            sb.AppendLine("## Inheritance");
            sb.AppendLine();
            StringBuilder chain = new();
            chain.Append(ResolveTypeLink("Object", "System.Object"));
            chain.Append(" \u2192 ");
            chain.Append(ResolveTypeLink(type.BaseType, type.BaseTypeFullName));
            chain.Append(" \u2192 ");
            chain.Append($"**{type.Name}**");
            sb.AppendLine(chain.ToString());
            sb.AppendLine();
        }
        else if (type.Kind == "class")
        {
            sb.AppendLine("## Inheritance");
            sb.AppendLine();
            sb.AppendLine($"{ResolveTypeLink("Object", "System.Object")} \u2192 **{type.Name}**");
            sb.AppendLine();
        }

        // Implements
        if (type.InterfacesWithFullNames.Count > 0)
        {
            sb.AppendLine("## Implements");
            sb.AppendLine();
            string ifaceList = string.Join(", ", type.InterfacesWithFullNames.Select(
                i => ResolveTypeLink(i.DisplayName, i.FullName)));
            sb.AppendLine(ifaceList);
            sb.AppendLine();
        }

        // Implemented By (for interfaces)
        if (type.ImplementedBy.Count > 0)
        {
            sb.AppendLine("## Implemented By");
            sb.AppendLine();
            string implList = string.Join(", ", type.ImplementedBy.Select(
                i => ResolveTypeLink(i.DisplayName, i.FullName)));
            sb.AppendLine(implList);
            sb.AppendLine();
        }

        // ── Member summary tables ──────────────────────────────────────────
        bool hasLinks = nsSlug is not null && typeSlug is not null;

        // Constructors
        if (type.Constructors.Count > 0)
        {
            string? ctorUrl = hasLinks ? $"{baseUrl}/{GetMemberPageFileBase(nsSlug!, typeSlug!, "ctor")}.html" : null;
            sb.AppendLine("## Constructors");
            sb.AppendLine();
            sb.AppendLine("| Constructor | Description |");
            sb.AppendLine("|-------------|-------------|");
            if (type.Constructors.Count > 1)
            {
                // Collapse multiple overloads into a single row
                string summary = TruncateSummary(type.Constructors[0].Documentation?.Summary ?? string.Empty);
                string display = $"{type.Name}(...)";
                if (ctorUrl is not null)
                    sb.AppendLine($"| [{EscapeTableCell(display)}]({ctorUrl}) | {summary} |");
                else
                    sb.AppendLine($"| `{EscapeTableCell(display)}` | {summary} |");
            }
            else
            {
                MemberInfo ctor = type.Constructors[0];
                string sig = FormatShortSignature(ctor);
                string summary = TruncateSummary(ctor.Documentation?.Summary ?? string.Empty);
                if (ctorUrl is not null)
                    sb.AppendLine($"| [{EscapeTableCell(sig)}]({ctorUrl}#{Anchor(sig)}) | {summary} |");
                else
                    sb.AppendLine($"| `{EscapeTableCell(sig)}` | {summary} |");
            }
            sb.AppendLine();
        }

        // Properties
        if (type.Properties.Count > 0)
        {
            sb.AppendLine("## Properties");
            sb.AppendLine();
            sb.AppendLine("| Property | Type | Description |");
            sb.AppendLine("|----------|------|-------------|");
            foreach (IGrouping<string, MemberInfo> group in type.Properties.GroupBy(p => p.GroupKey).OrderBy(g => g.Key))
            {
                string? propUrl = hasLinks
                    ? $"{baseUrl}/{GetMemberPageFileBase(nsSlug!, typeSlug!, MemberToSlug(group.Key))}.html"
                    : null;
                List<MemberInfo> members = group.ToList();
                if (members.Count > 1)
                {
                    // Collapse indexer overloads into a single row
                    MemberInfo first = members[0];
                    string summary = TruncateSummary(first.Documentation?.Summary ?? string.Empty);
                    string display = first.Name.StartsWith("this[", StringComparison.Ordinal) ? "Item[...]" : first.Name;
                    string staticBadge = first.IsStatic ? " `static`" : "";
                    if (propUrl is not null)
                        sb.AppendLine($"| [{EscapeTableCell(display)}]({propUrl}){staticBadge} | | {summary} |");
                    else
                        sb.AppendLine($"| `{EscapeTableCell(display)}`{staticBadge} | | {summary} |");
                }
                else
                {
                    MemberInfo prop = members[0];
                    string summary = TruncateSummary(prop.Documentation?.Summary ?? string.Empty);
                    string staticBadge = prop.IsStatic ? " `static`" : "";
                    if (propUrl is not null)
                        sb.AppendLine($"| [{EscapeTableCell(prop.Name)}]({propUrl}){staticBadge} | {ResolveTypeLink(prop.ReturnType, prop.ReturnTypeFullName)} | {summary} |");
                    else
                        sb.AppendLine($"| `{EscapeTableCell(prop.Name)}`{staticBadge} | {ResolveTypeLink(prop.ReturnType, prop.ReturnTypeFullName)} | {summary} |");
                }
            }
            sb.AppendLine();
        }

        // Methods (grouped by name — overloads collapsed into a single row)
        if (type.Methods.Count > 0)
        {
            sb.AppendLine("## Methods");
            sb.AppendLine();
            sb.AppendLine("| Method | Description |");
            sb.AppendLine("|--------|-------------|");
            foreach (IGrouping<string, MemberInfo> group in type.Methods.GroupBy(m => m.GroupKey).OrderBy(g => g.Key))
            {
                string? methodUrl = hasLinks
                    ? $"{baseUrl}/{GetMemberPageFileBase(nsSlug!, typeSlug!, MemberToSlug(group.Key))}.html"
                    : null;
                List<MemberInfo> members = group.ToList();
                if (members.Count > 1)
                {
                    // Collapse overloads into a single row linking to the overload page
                    string summary = TruncateSummary(members[0].Documentation?.Summary ?? string.Empty);
                    string modifiers = members[0].IsStatic ? " `static`" : "";
                    if (methodUrl is not null)
                        sb.AppendLine($"| [{EscapeTableCell(group.Key)}]({methodUrl}){modifiers} | {summary} |");
                    else
                        sb.AppendLine($"| `{EscapeTableCell(group.Key)}`{modifiers} | {summary} |");
                }
                else
                {
                    MemberInfo method = members[0];
                    string sig = FormatShortSignature(method);
                    string summary = TruncateSummary(method.Documentation?.Summary ?? string.Empty);
                    string modifiers = method.IsStatic ? " `static`" : "";
                    if (methodUrl is not null)
                        sb.AppendLine($"| [{EscapeTableCell(sig)}]({methodUrl}#{Anchor(sig)}){modifiers} | {summary} |");
                    else
                        sb.AppendLine($"| `{EscapeTableCell(sig)}`{modifiers} | {summary} |");
                }
            }
            sb.AppendLine();
        }

        // Operators (grouped by CLR name — overloads collapsed into a single row)
        if (type.Operators.Count > 0)
        {
            sb.AppendLine("## Operators");
            sb.AppendLine();
            sb.AppendLine("| Operator | Description |");
            sb.AppendLine("|----------|-------------|");
            foreach (IGrouping<string, MemberInfo> group in type.Operators.GroupBy(m => m.GroupKey).OrderBy(g => g.Key))
            {
                string? opUrl = hasLinks
                    ? $"{baseUrl}/{GetMemberPageFileBase(nsSlug!, typeSlug!, MemberToSlug(group.Key))}.html"
                    : null;
                List<MemberInfo> members = group.ToList();
                if (members.Count > 1)
                {
                    // Collapse overloads into a single row
                    string displayName = GetOperatorGroupDisplayName(group.Key);
                    string summary = TruncateSummary(members[0].Documentation?.Summary ?? string.Empty);
                    if (opUrl is not null)
                        sb.AppendLine($"| [{EscapeTableCell(displayName)}]({opUrl}) | {summary} |");
                    else
                        sb.AppendLine($"| `{EscapeTableCell(displayName)}` | {summary} |");
                }
                else
                {
                    MemberInfo op = members[0];
                    string sig = FormatShortSignature(op);
                    string summary = TruncateSummary(op.Documentation?.Summary ?? string.Empty);
                    if (opUrl is not null)
                        sb.AppendLine($"| [{EscapeTableCell(sig)}]({opUrl}#{Anchor(sig)}) | {summary} |");
                    else
                        sb.AppendLine($"| `{EscapeTableCell(sig)}` | {summary} |");
                }
            }
            sb.AppendLine();
        }

        // Fields
        if (type.Fields.Count > 0)
        {
            sb.AppendLine("## Fields");
            sb.AppendLine();
            sb.AppendLine("| Field | Type | Description |");
            sb.AppendLine("|-------|------|-------------|");
            foreach (MemberInfo field in type.Fields)
            {
                string summary = TruncateSummary(field.Documentation?.Summary ?? string.Empty);
                string staticBadge = field.IsStatic ? " `static`" : "";
                if (hasLinks)
                {
                    string fieldUrl = $"{baseUrl}/{GetMemberPageFileBase(nsSlug!, typeSlug!, MemberToSlug(field.GroupKey))}.html";
                    sb.AppendLine($"| [{field.Name}]({fieldUrl}){staticBadge} | {ResolveTypeLink(field.ReturnType, field.ReturnTypeFullName)} | {summary} |");
                }
                else
                {
                    sb.AppendLine($"| `{field.Name}`{staticBadge} | {ResolveTypeLink(field.ReturnType, field.ReturnTypeFullName)} | {summary} |");
                }
            }
            sb.AppendLine();
        }

        // Events
        if (type.Events.Count > 0)
        {
            sb.AppendLine("## Events");
            sb.AppendLine();
            sb.AppendLine("| Event | Type | Description |");
            sb.AppendLine("|-------|------|-------------|");
            foreach (MemberInfo evt in type.Events)
            {
                string summary = TruncateSummary(evt.Documentation?.Summary ?? string.Empty);
                if (hasLinks)
                {
                    string evtUrl = $"{baseUrl}/{GetMemberPageFileBase(nsSlug!, typeSlug!, MemberToSlug(evt.GroupKey))}.html";
                    sb.AppendLine($"| [{evt.Name}]({evtUrl}) | {ResolveTypeLink(evt.ReturnType, evt.ReturnTypeFullName)} | {summary} |");
                }
                else
                {
                    sb.AppendLine($"| `{evt.Name}` | {ResolveTypeLink(evt.ReturnType, evt.ReturnTypeFullName)} | {summary} |");
                }
            }
            sb.AppendLine();
        }

        // Hand-authored examples (from file)
        if (!string.IsNullOrEmpty(typeExampleMarkdown))
        {
            sb.AppendLine("## Examples");
            sb.AppendLine();
            sb.AppendLine(typeExampleMarkdown);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(type.Documentation?.Example))
        {
            sb.AppendLine("## Example");
            sb.AppendLine();
            sb.AppendLine(type.Documentation!.Example);
            sb.AppendLine();
        }

        if (type.Documentation?.SeeAlso.Count > 0)
        {
            sb.AppendLine("## See Also");
            sb.AppendLine();
            foreach (string seeAlso in type.Documentation!.SeeAlso)
            {
                string shortName = XmlDocParser.GetShortTypeName(seeAlso);
                string? url = XmlDocParser.ResolveTypeUrl(seeAlso);
                if (url is not null)
                    sb.AppendLine($"- [`{shortName}`]({url})");
                else
                    sb.AppendLine($"- `{shortName}`");
            }
            sb.AppendLine();
        }

        WriteAppliesTo(sb, type.AvailableOnNetStandard20);
    }

    /// <summary>
    /// Writes the body of a member detail page. For member groups with multiple
    /// overloads, shows an overload summary table followed by individual sections.
    /// </summary>
    internal static void WriteMemberPageBody(
        StringBuilder sb,
        string ns,
        string nsSlug,
        TypeInfo type,
        string typeSlug,
        string memberDisplayName,
        string memberCategory,
        List<MemberInfo> members,
        SourceLinkResolver? sourceResolver = null,
        string? memberExampleMarkdown = null)
    {
        // Definition metadata — Namespace, Assembly, Source grouped together
        sb.AppendLine("## Definition");
        sb.AppendLine();
        sb.AppendLine($"**Namespace:** {ns}  ");
        sb.AppendLine($"**Assembly:** Corvus.Text.Json.dll  ");

        // For single members, include source in the Definition section
        if (members.Count == 1 && sourceResolver is not null)
        {
            string? srcUrl = sourceResolver.GetMemberSourceUrl(members[0].XmlDocKey);
            if (srcUrl is not null)
            {
                string displayPath = GetSourceDisplayPath(srcUrl);
                sb.AppendLine($"**Source:** [{displayPath}]({srcUrl})");
            }
        }

        sb.AppendLine();

        // For single members, show the detail directly
        if (members.Count == 1)
        {
            WriteMemberDetail(sb, members[0], 2, memberExampleMarkdown: memberExampleMarkdown);

            WriteAppliesTo(sb, members[0].AvailableOnNetStandard20);
            return;
        }

        // For multiple overloads, show an overloads summary table first
        sb.AppendLine("## Overloads");
        sb.AppendLine();
        sb.AppendLine($"| {memberCategory} | Description |");
        sb.AppendLine($"|{new string('-', memberCategory.Length + 2)}|-------------|");
        foreach (MemberInfo member in members)
        {
            string sig = FormatShortSignature(member);
            string summary = TruncateSummary(member.Documentation?.Summary ?? string.Empty);
            sb.AppendLine($"| [{EscapeTableCell(sig)}](#{Anchor(sig)}) | {summary} |");
        }
        sb.AppendLine();

        // Then show each overload in detail
        foreach (MemberInfo member in members)
        {
            string? srcUrl = sourceResolver?.GetMemberSourceUrl(member.XmlDocKey);
            WriteMemberDetail(sb, member, 2, useSignatureHeading: true, sourceUrl: srcUrl);

            WriteAppliesTo(sb, member.AvailableOnNetStandard20);

            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Hand-authored examples (from file) — shown once for the whole member group
        if (!string.IsNullOrEmpty(memberExampleMarkdown))
        {
            sb.AppendLine("## Examples");
            sb.AppendLine();
            sb.AppendLine(memberExampleMarkdown);
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Writes the "Applies To" section showing the target frameworks.
    /// </summary>
    private static void WriteAppliesTo(StringBuilder sb, bool availableOnNetStandard20 = true)
    {
        sb.AppendLine("## Applies To");
        sb.AppendLine();
        sb.AppendLine("| Product | Versions |");
        sb.AppendLine("|---------|----------|");
        sb.AppendLine("| .NET | 8, 9, 10 |");
        if (availableOnNetStandard20)
        {
            sb.AppendLine("| .NET Standard | 2.0 |");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Writes the detail for a single member (one overload of a method, a property, etc.).
    /// </summary>
    private static void WriteMemberDetail(StringBuilder sb, MemberInfo member, int headingLevel, bool useSignatureHeading = false, string? sourceUrl = null, string? memberExampleMarkdown = null)
    {
        string heading = new('#', headingLevel);

        string headingText = useSignatureHeading ? FormatShortSignature(member) : member.Name;
        string anchorId = Anchor(headingText);
        string escapedHeading = headingText.Replace("<", "&lt;").Replace(">", "&gt;");
        sb.AppendLine($"{heading} {escapedHeading} {{#{anchorId}}}");
        sb.AppendLine();

        if (sourceUrl is not null)
        {
            string displayPath = GetSourceDisplayPath(sourceUrl);
            sb.AppendLine($"**Source:** [{displayPath}]({sourceUrl})");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(member.Documentation?.Summary))
        {
            sb.AppendLine(member.Documentation!.Summary);
            sb.AppendLine();
        }

        sb.AppendLine("```csharp");
        sb.AppendLine(member.Signature);
        sb.AppendLine("```");
        sb.AppendLine();

        // Type parameters
        if (member.Documentation?.TypeParams.Count > 0)
        {
            sb.AppendLine($"{heading}# Type Parameters");
            sb.AppendLine();
            sb.AppendLine("| Parameter | Description |");
            sb.AppendLine("|-----------|-------------|");
            foreach (DocParam tp in member.Documentation!.TypeParams)
            {
                sb.AppendLine($"| `{tp.Name}` | {EscapeDescriptionForTable(tp.Description)} |");
            }
            sb.AppendLine();
        }

        // Parameters table
        if (member.Parameters.Count > 0)
        {
            sb.AppendLine($"{heading}# Parameters");
            sb.AppendLine();
            sb.AppendLine("| Name | Type | Description |");
            sb.AppendLine("|------|------|-------------|");
            foreach (ParameterInfo param in member.Parameters)
            {
                string desc = member.Documentation?.Params
                    .FirstOrDefault(p => p.Name == param.Name)?.Description ?? string.Empty;
                string optionalSuffix = param.IsOptional ? " *(optional)*" : "";
                sb.AppendLine($"| `{param.Name}` | {ResolveTypeLink(param.Type, param.TypeFullName)} | {EscapeDescriptionForTable(desc)}{optionalSuffix} |");
            }
            sb.AppendLine();
        }

        // Returns
        if (!string.IsNullOrEmpty(member.ReturnType) && member.ReturnType != "void")
        {
            string returnsDesc = member.Documentation?.Returns ?? string.Empty;
            sb.AppendLine($"{heading}# Returns");
            sb.AppendLine();
            sb.AppendLine(ResolveTypeLink(member.ReturnType, member.ReturnTypeFullName));
            if (!string.IsNullOrEmpty(returnsDesc))
            {
                sb.AppendLine();
                sb.AppendLine(returnsDesc);
            }
            sb.AppendLine();
        }

        // Property value (for properties, shows the value description)
        if (!string.IsNullOrEmpty(member.Documentation?.Value))
        {
            sb.AppendLine($"{heading}# Property Value");
            sb.AppendLine();
            sb.AppendLine(ResolveTypeLink(member.ReturnType, member.ReturnTypeFullName));
            sb.AppendLine();
            sb.AppendLine(member.Documentation!.Value);
            sb.AppendLine();
        }

        // Exceptions
        if (member.Documentation?.Exceptions.Count > 0)
        {
            sb.AppendLine($"{heading}# Exceptions");
            sb.AppendLine();
            sb.AppendLine("| Exception | Description |");
            sb.AppendLine("|-----------|-------------|");
            foreach (DocException ex in member.Documentation!.Exceptions)
            {
                string exShortName = XmlDocParser.GetShortTypeName(ex.Type);
                sb.AppendLine($"| {ResolveTypeLink(exShortName, ex.Type)} | {EscapeDescriptionForTable(ex.Description)} |");
            }
            sb.AppendLine();
        }

        // Implements (interface implementations)
        if (member.Implements.Count > 0)
        {
            sb.AppendLine($"{heading}# Implements");
            sb.AppendLine();
            foreach (ImplementsInfo impl in member.Implements)
            {
                string displayText = $"{impl.InterfaceDisplayName}.{impl.MemberName}";
                string memberLink = ResolveInterfaceMemberLink(displayText, impl.InterfaceFullName, impl.MemberName);
                sb.AppendLine(memberLink);
                sb.AppendLine();
            }
        }

        // Hand-authored examples (from file)
        if (!string.IsNullOrEmpty(memberExampleMarkdown))
        {
            sb.AppendLine($"{heading}# Examples");
            sb.AppendLine();
            sb.AppendLine(memberExampleMarkdown);
            sb.AppendLine();
        }

        // Remarks
        if (!string.IsNullOrEmpty(member.Documentation?.Remarks))
        {
            sb.AppendLine($"{heading}# Remarks");
            sb.AppendLine();
            sb.AppendLine(member.Documentation!.Remarks);
            sb.AppendLine();
        }
    }

    private static string BuildTypeDeclaration(TypeInfo type)
    {
        StringBuilder sb = new();

        if (type.IsStatic)
        {
            sb.Append("public static ");
        }
        else if (type.IsAbstract && type.Kind == "class")
        {
            sb.Append("public abstract ");
        }
        else if (type.IsSealed && type.Kind == "class")
        {
            sb.Append("public sealed ");
        }
        else
        {
            sb.Append("public ");
        }

        // readonly struct
        if (type.Kind == "struct")
        {
            sb.Append("readonly ");
        }

        sb.Append(type.Kind);
        sb.Append(' ');
        sb.Append(type.Name);

        // Base type and interfaces
        List<string> baseList = [];
        if (type.BaseType is not null)
        {
            baseList.Add(type.BaseType);
        }

        baseList.AddRange(type.Interfaces);

        if (baseList.Count > 0)
        {
            sb.Append(" : ");
            sb.Append(string.Join(", ", baseList));
        }

        // Generic constraints
        foreach (GenericConstraintInfo constraint in type.GenericConstraints)
        {
            sb.AppendLine();
            sb.Append($"    where {constraint.ParameterName} : {string.Join(", ", constraint.Constraints)}");
        }

        return sb.ToString();
    }

    internal static string NamespaceToFileName(string ns)
    {
        return ns.Replace('.', '-').ToLowerInvariant();
    }

    internal static string TypeToSlug(string typeName)
    {
        return typeName
            .Replace('.', '-')
            .Replace('<', '-')
            .Replace(">", "")
            .Replace(", ", "-")
            .Replace(",", "-")
            .Replace(' ', '-')
            .ToLowerInvariant()
            .TrimEnd('-');
    }

    /// <summary>
    /// Converts a member group key to a URL-safe slug.
    /// </summary>
    internal static string MemberToSlug(string groupKey)
    {
        return groupKey
            .Replace(".", "")
            .Replace("_", "-")
            .Replace('<', '-')
            .Replace(">", "")
            .Replace(", ", "-")
            .Replace(",", "-")
            .Replace(' ', '-')
            .ToLowerInvariant()
            .TrimEnd('-');
    }

    /// <summary>
    /// Builds the file base name for a member detail page (without extension).
    /// Pattern: {nsSlug}-{typeSlug}.{memberSlug}
    /// </summary>
    internal static string GetMemberPageFileBase(string nsSlug, string typeSlug, string memberSlug)
    {
        return $"{nsSlug}-{typeSlug}.{memberSlug}";
    }

    /// <summary>
    /// Formats a short signature for use in summary tables.
    /// Example: "Parse(String, JsonDocumentOptions)" or "implicit operator Source(Mutable)"
    /// </summary>
    private static string FormatShortSignature(MemberInfo member)
    {
        // For indexers, the Name already contains the parameter types (e.g. "this[int]")
        if (member.Name.StartsWith("this[", StringComparison.Ordinal))
        {
            return member.Name;
        }

        string paramTypes = string.Join(", ", member.Parameters.Select(p => p.Type));
        return member.Parameters.Count > 0
            ? $"{member.Name}({paramTypes})"
            : $"{member.Name}()";
    }

    /// <summary>
    /// Escapes characters that would break markdown table cells or link syntax.
    /// </summary>
    private static string EscapeTableCell(string text)
    {
        return text
            .Replace("|", "\\|")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>
    /// Escapes bare brackets in description text that already contains markdown links.
    /// Preserves existing [...](url) and [`code`](url) patterns from see-cref resolution,
    /// but escapes standalone [ and ] that would confuse the markdown parser.
    /// </summary>
    private static string EscapeDescriptionForTable(string text)
    {
        // Replace | for table cell safety
        text = text.Replace("|", "\\|");

        // Match markdown links: [`code`](url) or [text](url) where text has no unbalanced brackets.
        // Process right-to-left so indices remain valid after replacement.
        var links = new List<(int Start, int Length, string Value)>();
        // Match backtick-wrapped links first: [`...`](url)
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
            text, @"\[`[^`]+`\]\([^)]+\)"))
        {
            links.Add((m.Index, m.Length, m.Value));
        }

        // Mark matched ranges to avoid re-matching
        bool[] matched = new bool[text.Length];
        foreach (var link in links)
        {
            for (int i = link.Start; i < link.Start + link.Length; i++)
                matched[i] = true;
        }

        // Match plain text links: [text](url) where text contains no [ or ]
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
            text, @"\[([^\[\]]+)\]\([^)]+\)"))
        {
            if (!matched[m.Index])
            {
                links.Add((m.Index, m.Length, m.Value));
            }
        }

        // Sort by position descending so we can replace from end to start
        links.Sort((a, b) => b.Start.CompareTo(a.Start));

        // Replace links with placeholders
        char[] chars = text.ToCharArray();
        var sb = new StringBuilder(text.Length);
        int pos = 0;
        // Re-sort ascending for forward iteration
        links.Sort((a, b) => a.Start.CompareTo(b.Start));

        foreach (var link in links)
        {
            // Append and escape text before this link
            for (int i = pos; i < link.Start; i++)
            {
                char c = chars[i];
                if (c == '[') sb.Append("\\[");
                else if (c == ']') sb.Append("\\]");
                else sb.Append(c);
            }
            // Append the link unchanged
            sb.Append(link.Value);
            pos = link.Start + link.Length;
        }

        // Append and escape remaining text
        for (int i = pos; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c == '[') sb.Append("\\[");
            else if (c == ']') sb.Append("\\]");
            else sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a human-readable group display name for an operator CLR method name.
    /// </summary>
    internal static string GetOperatorGroupDisplayName(string clrName)
    {
        return clrName switch
        {
            "op_Implicit" => "Implicit",
            "op_Explicit" => "Explicit",
            "op_Equality" => "Equality",
            "op_Inequality" => "Inequality",
            "op_LessThan" => "LessThan",
            "op_GreaterThan" => "GreaterThan",
            "op_LessThanOrEqual" => "LessThanOrEqual",
            "op_GreaterThanOrEqual" => "GreaterThanOrEqual",
            "op_Addition" => "Addition",
            "op_Subtraction" => "Subtraction",
            "op_Multiply" or "op_Multiplication" => "Multiply",
            "op_Division" => "Division",
            "op_Modulus" => "Modulus",
            "op_UnaryPlus" => "UnaryPlus",
            "op_UnaryNegation" => "UnaryNegation",
            "op_Increment" => "Increment",
            "op_Decrement" => "Decrement",
            _ => clrName.Replace("op_", ""),
        };
    }

    private static string Anchor(string name)
    {
        // Produce a clean URL-safe anchor: lowercase, collapse non-alphanumeric to single hyphens
        StringBuilder sb = new();
        bool lastWasHyphen = false;
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                sb.Append('-');
                lastWasHyphen = true;
            }
        }

        return sb.ToString().Trim('-');
    }

    private static string TruncateSummary(string summary)
    {
        if (summary.Length <= 200)
        {
            return EscapeDescriptionForTable(summary);
        }

        return EscapeDescriptionForTable(summary[..197]) + "...";
    }

    private static string EscapeYamlString(string value)
    {
        return value.Replace("\"", "\\\"");
    }

    /// <summary>
    /// Extracts the file name from a GitHub source URL for display.
    /// </summary>
    private static string GetSourceDisplayPath(string url)
    {
        int hashIdx = url.IndexOf('#');
        string urlWithoutFragment = hashIdx >= 0 ? url[..hashIdx] : url;
        int lastSlash = urlWithoutFragment.LastIndexOf('/');
        return lastSlash >= 0 ? urlWithoutFragment[(lastSlash + 1)..] : urlWithoutFragment;
    }

    /// <summary>
    /// Creates a markdown link for a type reference, using local URLs for Corvus types,
    /// learn.microsoft.com for BCL types, or plain code formatting as a fallback.
    /// </summary>
    private static string ResolveTypeLink(string displayName, string? fullName)
    {
        string escaped = displayName.Replace("|", "\\|");

        if (fullName is not null)
        {
            // Try local type URL first
            string? localUrl = XmlDocParser.ResolveTypeUrl(fullName);
            if (localUrl is not null)
            {
                return $"[`{escaped}`]({localUrl})";
            }

            // Try BCL URL for System.* and Microsoft.* types
            if (fullName.StartsWith("System.", StringComparison.Ordinal) ||
                fullName.StartsWith("Microsoft.", StringComparison.Ordinal))
            {
                string bclUrl = GetBclTypeUrl(fullName);
                return $"[`{escaped}`]({bclUrl})";
            }

            // Try NodaTime URL
            if (fullName.StartsWith("NodaTime.", StringComparison.Ordinal))
            {
                string nodaUrl = GetNodaTimeTypeUrl(fullName);
                return $"[`{escaped}`]({nodaUrl})";
            }
        }

        return $"`{escaped}`";
    }

    /// <summary>
    /// Resolves a link to a specific member on an interface type.
    /// For BCL types: https://learn.microsoft.com/dotnet/api/system.idisposable.dispose
    /// For local types: links to the member page in our API docs.
    /// </summary>
    private static string ResolveInterfaceMemberLink(string displayText, string? interfaceFullName, string memberName)
    {
        string escaped = displayText.Replace("|", "\\|").Replace("<", "&lt;").Replace(">", "&gt;");

        if (interfaceFullName is not null)
        {
            // Strip generic arity for URL construction (e.g. "IEquatable`1" → "IEquatable")
            string baseFullName = interfaceFullName;
            int backtick = baseFullName.IndexOf('`');
            if (backtick >= 0)
            {
                baseFullName = baseFullName[..backtick];
            }

            // Try local type URL first — link to the member page
            string? localUrl = XmlDocParser.ResolveTypeUrl(interfaceFullName);
            if (localUrl is not null)
            {
                // Convert type page URL to member page URL
                // e.g. /api/corvus-text-json-internal-ijsonelement.html → /api/corvus-text-json-internal-ijsonelement.dispose.html
                string memberUrl = localUrl.Replace(".html", $".{memberName.ToLowerInvariant()}.html");
                return $"[`{escaped}`]({memberUrl})";
            }

            // BCL types
            if (interfaceFullName.StartsWith("System.", StringComparison.Ordinal) ||
                interfaceFullName.StartsWith("Microsoft.", StringComparison.Ordinal))
            {
                string urlName = baseFullName.ToLowerInvariant();
                string memberUrl = $"https://learn.microsoft.com/dotnet/api/{urlName}.{memberName.ToLowerInvariant()}";
                return $"[`{escaped}`]({memberUrl})";
            }

            // NodaTime types
            if (interfaceFullName.StartsWith("NodaTime.", StringComparison.Ordinal))
            {
                string urlName = interfaceFullName.Replace('`', '-');
                string memberUrl = $"https://nodatime.org/3.2.x/api/{urlName}.html";
                return $"[`{escaped}`]({memberUrl})";
            }
        }

        return $"`{escaped}`";
    }

    /// <summary>
    /// Generates a learn.microsoft.com URL for a BCL type.
    /// </summary>
    private static string GetBclTypeUrl(string fullName)
    {
        // System.Collections.Generic.List`1 → system.collections.generic.list-1
        string urlName = fullName.ToLowerInvariant().Replace('`', '-');
        return $"https://learn.microsoft.com/dotnet/api/{urlName}";
    }

    /// <summary>
    /// Generates a nodatime.org API URL for a NodaTime type.
    /// </summary>
    private static string GetNodaTimeTypeUrl(string fullName)
    {
        // NodaTime.Period → NodaTime.Period.html
        // NodaTime.Text.OffsetDateTimePattern → NodaTime.Text.OffsetDateTimePattern.html
        // Strip generic arity suffix if present: NodaTime.SomeType`1 → NodaTime.SomeType
        int backtick = fullName.IndexOf('`');
        string cleanName = backtick >= 0 ? fullName[..backtick] : fullName;
        return $"https://www.nodatime.org/3.3.x/api/{cleanName}.html";
    }
}
