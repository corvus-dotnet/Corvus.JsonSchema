using System.Text;

namespace XmlDocToMarkdown;

/// <summary>
/// Generates Vellum taxonomy YAML files for each namespace page.
/// </summary>
public sealed class TaxonomyGenerator(string taxonomyOutputDir, string contentOutputDir, string baseUrl, string templateName)
{
    public void Generate(Dictionary<string, NamespaceInfo> namespaces)
    {
        int rank = 1;
        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            string fileName = MarkdownGenerator.NamespaceToFileName(ns);
            string yamlPath = Path.Combine(taxonomyOutputDir, fileName + ".yml");

            // Compute the relative path from taxonomy to content
            string taxonomyDir = Path.GetFullPath(taxonomyOutputDir);
            string contentDir = Path.GetFullPath(contentOutputDir);
            string relativePath = Path.GetRelativePath(taxonomyDir, contentDir).Replace('\\', '/');
            string contentRelativePath = $"{relativePath}/{fileName}.md";

            StringBuilder sb = new();
            sb.AppendLine($"ContentType: application/vnd.endjin.ssg.page+yaml");
            sb.AppendLine($"Title: \"{EscapeYaml(ns)} Namespace\"");
            sb.AppendLine($"Template: {templateName}");
            sb.AppendLine($"Navigation:");
            sb.AppendLine($"  Title: {ns}");
            sb.AppendLine($"  Description: \"API reference for the {ns} namespace\"");
            sb.AppendLine($"  Parent: {baseUrl}");
            sb.AppendLine($"  Url: {baseUrl}/{fileName}.html");
            sb.AppendLine($"  Rank: {rank}");
            sb.AppendLine($"MetaData:");
            sb.AppendLine($"  Title: \"{EscapeYaml(ns)} Namespace — API Reference\"");
            sb.AppendLine($"  Description: \"API documentation for the {ns} namespace\"");
            sb.AppendLine($"  Keywords: [API, reference, {ns}]");
            sb.AppendLine($"OpenGraph:");
            sb.AppendLine($"  Title: \"{EscapeYaml(ns)} Namespace\"");
            sb.AppendLine($"  Description: \"API reference for the {ns} namespace\"");
            sb.AppendLine($"  Image:");
            sb.AppendLine($"ContentBlocks:");
            sb.AppendLine($"  - ContentType: application/vnd.endjin.ssg.content+md");
            sb.AppendLine($"    Spec:");
            sb.AppendLine($"      Path: {contentRelativePath}");

            File.WriteAllText(yamlPath, sb.ToString());

            rank++;
        }
    }

    public void GeneratePerType(Dictionary<string, NamespaceInfo> namespaces)
    {
        string taxonomyDir = Path.GetFullPath(taxonomyOutputDir);
        string contentDir = Path.GetFullPath(contentOutputDir);
        string relativePath = Path.GetRelativePath(taxonomyDir, contentDir).Replace('\\', '/');

        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            string nsSlug = MarkdownGenerator.NamespaceToFileName(ns);

            int rank = 1;
            foreach (TypeInfo type in kvp.Value.Types)
            {
                string typeSlug = MarkdownGenerator.TypeToSlug(type.Name);
                string fileBase = $"{nsSlug}-{typeSlug}";
                string yamlPath = Path.Combine(taxonomyOutputDir, fileBase + ".yml");
                string contentRelativePath = $"{relativePath}/{fileBase}.md";

                string summary = type.Documentation?.Summary ?? $"{type.Name} {type.Kind}";
                if (summary.Length > 200) summary = summary[..197] + "...";

                StringBuilder sb = new();
                sb.AppendLine($"ContentType: application/vnd.endjin.ssg.page+yaml");
                sb.AppendLine($"Title: \"{EscapeYaml(type.Name)}\"");
                sb.AppendLine($"Template: {templateName}");
                sb.AppendLine($"Navigation:");
                sb.AppendLine($"  Title: \"{EscapeYaml(type.Name)}\"");
                sb.AppendLine($"  Description: \"{EscapeYaml(summary)}\"");
                sb.AppendLine($"  Parent: {baseUrl}");
                sb.AppendLine($"  Url: {baseUrl}/{fileBase}.html");
                sb.AppendLine($"  Rank: {100 + rank}");
                sb.AppendLine($"MetaData:");
                sb.AppendLine($"  Title: \"{EscapeYaml(type.Name)} \u2014 {ns}\"");
                sb.AppendLine($"  Description: \"API documentation for {EscapeYaml(type.Name)} in {ns}\"");
                sb.AppendLine($"  Keywords: [API, {type.Kind}, \"{EscapeYaml(type.Name)}\", {ns}]");
                sb.AppendLine($"OpenGraph:");
                sb.AppendLine($"  Title: \"{EscapeYaml(type.Name)} \u2014 {ns}\"");
                sb.AppendLine($"  Description: \"API documentation for {EscapeYaml(type.Name)}\"");
                sb.AppendLine($"  Image:");
                sb.AppendLine($"ContentBlocks:");
                sb.AppendLine($"  - ContentType: application/vnd.endjin.ssg.content+md");
                sb.AppendLine($"    Spec:");
                sb.AppendLine($"      Path: {contentRelativePath}");

                File.WriteAllText(yamlPath, sb.ToString());
                rank++;
            }
        }
    }

    public int GeneratePerMember(Dictionary<string, NamespaceInfo> namespaces)
    {
        int count = 0;
        string taxonomyDir = Path.GetFullPath(taxonomyOutputDir);
        string contentDir = Path.GetFullPath(contentOutputDir);
        string relativePath = Path.GetRelativePath(taxonomyDir, contentDir).Replace('\\', '/');

        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            string nsSlug = MarkdownGenerator.NamespaceToFileName(ns);

            foreach (TypeInfo type in kvp.Value.Types)
            {
                string typeSlug = MarkdownGenerator.TypeToSlug(type.Name);

                // Constructors (all on one page)
                if (type.Constructors.Count > 0)
                {
                    WriteMemberTaxonomy(taxonomyDir, relativePath, ns, nsSlug, type, typeSlug,
                        "ctor", $"{type.Name} Constructors", "Constructor");
                    count++;
                }

                // Properties (grouped by name)
                foreach (IGrouping<string, MemberInfo> group in type.Properties.GroupBy(p => p.GroupKey))
                {
                    WriteMemberTaxonomy(taxonomyDir, relativePath, ns, nsSlug, type, typeSlug,
                        MarkdownGenerator.MemberToSlug(group.Key), $"{type.Name}.{group.Key} Property", "Property");
                    count++;
                }

                // Methods (grouped by name)
                foreach (IGrouping<string, MemberInfo> group in type.Methods.GroupBy(m => m.GroupKey))
                {
                    WriteMemberTaxonomy(taxonomyDir, relativePath, ns, nsSlug, type, typeSlug,
                        MarkdownGenerator.MemberToSlug(group.Key), $"{type.Name}.{group.Key} Method", "Method");
                    count++;
                }

                // Operators (grouped by CLR name)
                foreach (IGrouping<string, MemberInfo> group in type.Operators.GroupBy(m => m.GroupKey))
                {
                    string displayGroupName = MarkdownGenerator.GetOperatorGroupDisplayName(group.Key);
                    WriteMemberTaxonomy(taxonomyDir, relativePath, ns, nsSlug, type, typeSlug,
                        MarkdownGenerator.MemberToSlug(group.Key), $"{type.Name}.{displayGroupName} Operator", "Operator");
                    count++;
                }

                // Fields (each on its own page)
                foreach (MemberInfo field in type.Fields)
                {
                    WriteMemberTaxonomy(taxonomyDir, relativePath, ns, nsSlug, type, typeSlug,
                        MarkdownGenerator.MemberToSlug(field.GroupKey), $"{type.Name}.{field.Name} Field", "Field");
                    count++;
                }

                // Events (each on its own page)
                foreach (MemberInfo evt in type.Events)
                {
                    WriteMemberTaxonomy(taxonomyDir, relativePath, ns, nsSlug, type, typeSlug,
                        MarkdownGenerator.MemberToSlug(evt.GroupKey), $"{type.Name}.{evt.Name} Event", "Event");
                    count++;
                }
            }
        }

        return count;
    }

    private void WriteMemberTaxonomy(
        string taxonomyDir, string relativePath,
        string ns, string nsSlug, TypeInfo type, string typeSlug,
        string memberSlug, string pageTitle, string memberCategory)
    {
        string fileBase = MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, memberSlug);
        string yamlPath = Path.Combine(taxonomyDir, fileBase + ".yml");
        string contentRelativePath = $"{relativePath}/{fileBase}.md";

        string summary = $"{pageTitle} — {ns}";
        if (summary.Length > 200)
        {
            summary = summary[..197] + "...";
        }

        StringBuilder sb = new();
        sb.AppendLine($"ContentType: application/vnd.endjin.ssg.page+yaml");
        sb.AppendLine($"Title: \"{EscapeYaml(pageTitle)}\"");
        sb.AppendLine($"Template: {templateName}");
        sb.AppendLine($"Navigation:");
        sb.AppendLine($"  Title: \"{EscapeYaml(pageTitle)}\"");
        sb.AppendLine($"  Description: \"{EscapeYaml(summary)}\"");
        sb.AppendLine($"  Parent: {baseUrl}");
        sb.AppendLine($"  Url: {baseUrl}/{fileBase}.html");
        sb.AppendLine($"  Rank: 1000");
        sb.AppendLine($"MetaData:");
        sb.AppendLine($"  Title: \"{EscapeYaml(pageTitle)} — {ns}\"");
        sb.AppendLine($"  Description: \"API documentation for {EscapeYaml(pageTitle)} in {ns}\"");
        sb.AppendLine($"  Keywords: [API, {memberCategory}, \"{EscapeYaml(type.Name)}\", {ns}]");
        sb.AppendLine($"OpenGraph:");
        sb.AppendLine($"  Title: \"{EscapeYaml(pageTitle)} — {ns}\"");
        sb.AppendLine($"  Description: \"API documentation for {EscapeYaml(pageTitle)}\"");
        sb.AppendLine($"  Image:");
        sb.AppendLine($"ContentBlocks:");
        sb.AppendLine($"  - ContentType: application/vnd.endjin.ssg.content+md");
        sb.AppendLine($"    Spec:");
        sb.AppendLine($"      Path: {contentRelativePath}");

        File.WriteAllText(yamlPath, sb.ToString());
    }

    private static string EscapeYaml(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", " ")
            .Replace("\r", "");
    }
}
