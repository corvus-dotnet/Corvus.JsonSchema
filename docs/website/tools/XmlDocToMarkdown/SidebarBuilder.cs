using System.Text;
using System.Web;

namespace XmlDocToMarkdown;

/// <summary>
/// Builds the hierarchical API sidebar used by both the Vellum view generator
/// and the standalone HTML page generator. Matches the .NET reference docs
/// pattern: Namespace → Type → Member categories → Individual members.
/// </summary>
internal static class SidebarBuilder
{
    /// <summary>
    /// Appends the full hierarchical sidebar markup to <paramref name="sb"/>.
    /// Each namespace becomes a collapsible section; types are listed beneath
    /// the namespace. The active type expands to show its members grouped by
    /// category (Constructors, Properties, Methods, Operators, Fields, Events).
    /// </summary>
    public static void AppendSidebar(
        StringBuilder sb,
        Dictionary<string, NamespaceInfo> namespaces,
        string? currentNsSlug,
        string? currentTypeFileBase,
        string baseUrl,
        string? currentMemberFileBase = null)
    {
        // Mobile toggle button and backdrop — matches the docs page structure
        sb.AppendLine("    <button class=\"sidebar-toggle\" aria-label=\"Toggle navigation\" aria-expanded=\"false\"></button>");
        sb.AppendLine("    <div class=\"sidebar-backdrop\"></div>");
        sb.AppendLine("    <aside class=\"sidebar\" data-has-member-nav>");

        // Search box — sits above the scrollable tree
        sb.AppendLine("        <div class=\"sidebar-search\">");
        sb.AppendLine("            <svg class=\"sidebar-search__icon\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\" fill=\"currentColor\"><path d=\"M11.5 7a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0Zm-.82 4.74a6 6 0 1 1 1.06-1.06l3.04 3.04a.75.75 0 1 1-1.06 1.06l-3.04-3.04Z\"/></svg>");
        sb.AppendLine("            <input id=\"sidebar-search-input\" class=\"sidebar-search__input\" type=\"search\" placeholder=\"Search\" autocomplete=\"off\" />");
        sb.AppendLine("            <div id=\"sidebar-search-dropdown\" class=\"sidebar-search__dropdown\" hidden></div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"sidebar__inner\">");

        foreach (KeyValuePair<string, NamespaceInfo> kvp in namespaces.OrderBy(n => n.Key))
        {
            string ns = kvp.Key;
            string nsSlug = MarkdownGenerator.NamespaceToFileName(ns);
            bool isCurrentNs = nsSlug == currentNsSlug;

            sb.AppendLine("            <div class=\"sidebar__section\">");
            sb.AppendLine($"                <button class=\"sidebar__heading{(isCurrentNs ? "" : " is-collapsed")}\">{HtmlEncode(ns)}</button>");
            sb.AppendLine($"                <div class=\"sidebar__body{(isCurrentNs ? "" : " is-collapsed")}\">");
            sb.AppendLine("                    <ul class=\"sidebar__list\">");

            // Namespace overview link
            string nsActive = (isCurrentNs && currentTypeFileBase is null) ? " is-active" : "";
            sb.AppendLine($"                        <li class=\"sidebar__item\"><a class=\"sidebar__link{nsActive}\" href=\"{baseUrl}/{nsSlug}.html\"><strong>Overview</strong></a></li>");

            // Type links — always include member trees (collapsed unless active)
            foreach (TypeInfo type in kvp.Value.Types.OrderBy(t => t.Name))
            {
                string typeSlug = MarkdownGenerator.TypeToSlug(type.Name);
                string fileBase = $"{nsSlug}-{typeSlug}";
                bool isActiveType = fileBase == currentTypeFileBase;

                // Type link: on the type page it's is-active; on a member page
                // it's is-current (visually highlighted but not "active")
                string typeClass = isActiveType
                    ? (currentMemberFileBase is null ? " is-active" : " is-current")
                    : "";
                sb.AppendLine($"                        <li class=\"sidebar__item\">");
                sb.AppendLine($"                            <a class=\"sidebar__link sidebar__link--type{typeClass}\" href=\"{baseUrl}/{fileBase}.html\">{HtmlEncodeWithBreaks(type.Name)}</a>");

                // Always include member tree; collapsed unless this is the active type
                AppendMemberTree(sb, type, nsSlug, typeSlug, baseUrl, isActiveType ? currentMemberFileBase : null, collapsed: !isActiveType);

                sb.AppendLine($"                        </li>");
            }

            sb.AppendLine("                    </ul>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
        }

        sb.AppendLine("        </div>");
        sb.AppendLine("    </aside>");
    }

    /// <summary>
    /// Renders the member sub-tree for a type. Each member category
    /// (Constructors, Properties, Methods, etc.) is a collapsible node
    /// matching the MS Docs tree pattern. A category is expanded when it
    /// contains the currently-active member; otherwise it is collapsed.
    /// </summary>
    private static void AppendMemberTree(
        StringBuilder sb,
        TypeInfo type,
        string nsSlug,
        string typeSlug,
        string baseUrl,
        string? currentMemberFileBase,
        bool collapsed = false)
    {
        string hiddenAttr = collapsed ? " hidden" : "";
        sb.AppendLine($"                            <ul class=\"sidebar__members\"{hiddenAttr}>");

        // Constructors
        if (type.Constructors.Count > 0)
        {
            string ctorFileBase = MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, "ctor");
            bool categoryActive = ctorFileBase == currentMemberFileBase;
            AppendCategoryStart(sb, "Constructors", categoryActive);
            string ctorActive = categoryActive ? " is-active" : "";
            sb.AppendLine($"                                        <li class=\"sidebar__member\"><a class=\"sidebar__link sidebar__link--member{ctorActive}\" href=\"{baseUrl}/{ctorFileBase}.html\">{HtmlEncodeWithBreaks(type.Constructors[0].Name)}</a></li>");
            AppendCategoryEnd(sb);
        }

        // Properties
        if (type.Properties.Count > 0)
        {
            bool categoryActive = IsCategoryActive(type.Properties, nsSlug, typeSlug, currentMemberFileBase);
            AppendCategoryStart(sb, "Properties", categoryActive);
            foreach (IGrouping<string, MemberInfo> group in type.Properties.GroupBy(p => p.GroupKey).OrderBy(g => g.Key))
            {
                string memberSlug = MarkdownGenerator.MemberToSlug(group.Key);
                string fileBase = MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, memberSlug);
                string active = fileBase == currentMemberFileBase ? " is-active" : "";
                string displayName = group.Count() > 1 && group.Key == "Item" ? "Item[]" : group.First().Name;
                sb.AppendLine($"                                        <li class=\"sidebar__member\"><a class=\"sidebar__link sidebar__link--member{active}\" href=\"{baseUrl}/{fileBase}.html\">{HtmlEncodeWithBreaks(displayName)}</a></li>");
            }
            AppendCategoryEnd(sb);
        }

        // Methods
        if (type.Methods.Count > 0)
        {
            bool categoryActive = IsCategoryActive(type.Methods, nsSlug, typeSlug, currentMemberFileBase);
            AppendCategoryStart(sb, "Methods", categoryActive);
            foreach (IGrouping<string, MemberInfo> group in type.Methods.GroupBy(m => m.GroupKey).OrderBy(g => g.Key))
            {
                string memberSlug = MarkdownGenerator.MemberToSlug(group.Key);
                string fileBase = MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, memberSlug);
                string active = fileBase == currentMemberFileBase ? " is-active" : "";
                sb.AppendLine($"                                        <li class=\"sidebar__member\"><a class=\"sidebar__link sidebar__link--member{active}\" href=\"{baseUrl}/{fileBase}.html\">{HtmlEncodeWithBreaks(group.Key)}</a></li>");
            }
            AppendCategoryEnd(sb);
        }

        // Operators
        if (type.Operators.Count > 0)
        {
            bool categoryActive = IsCategoryActive(type.Operators, nsSlug, typeSlug, currentMemberFileBase);
            AppendCategoryStart(sb, "Operators", categoryActive);
            foreach (IGrouping<string, MemberInfo> group in type.Operators.GroupBy(m => m.GroupKey).OrderBy(g => g.Key))
            {
                string memberSlug = MarkdownGenerator.MemberToSlug(group.Key);
                string fileBase = MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, memberSlug);
                string active = fileBase == currentMemberFileBase ? " is-active" : "";
                string displayName = GetOperatorGroupDisplayName(group.Key);
                sb.AppendLine($"                                        <li class=\"sidebar__member\"><a class=\"sidebar__link sidebar__link--member{active}\" href=\"{baseUrl}/{fileBase}.html\">{HtmlEncodeWithBreaks(displayName)}</a></li>");
            }
            AppendCategoryEnd(sb);
        }

        // Fields
        if (type.Fields.Count > 0)
        {
            bool categoryActive = IsCategoryActive(type.Fields, nsSlug, typeSlug, currentMemberFileBase);
            AppendCategoryStart(sb, "Fields", categoryActive);
            foreach (MemberInfo field in type.Fields.OrderBy(f => f.Name))
            {
                string memberSlug = MarkdownGenerator.MemberToSlug(field.GroupKey);
                string fileBase = MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, memberSlug);
                string active = fileBase == currentMemberFileBase ? " is-active" : "";
                sb.AppendLine($"                                        <li class=\"sidebar__member\"><a class=\"sidebar__link sidebar__link--member{active}\" href=\"{baseUrl}/{fileBase}.html\">{HtmlEncodeWithBreaks(field.Name)}</a></li>");
            }
            AppendCategoryEnd(sb);
        }

        // Events
        if (type.Events.Count > 0)
        {
            bool categoryActive = IsCategoryActive(type.Events, nsSlug, typeSlug, currentMemberFileBase);
            AppendCategoryStart(sb, "Events", categoryActive);
            foreach (MemberInfo evt in type.Events.OrderBy(e => e.Name))
            {
                string memberSlug = MarkdownGenerator.MemberToSlug(evt.GroupKey);
                string fileBase = MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, memberSlug);
                string active = fileBase == currentMemberFileBase ? " is-active" : "";
                sb.AppendLine($"                                        <li class=\"sidebar__member\"><a class=\"sidebar__link sidebar__link--member{active}\" href=\"{baseUrl}/{fileBase}.html\">{HtmlEncodeWithBreaks(evt.Name)}</a></li>");
            }
            AppendCategoryEnd(sb);
        }

        sb.AppendLine("                            </ul>");
    }

    /// <summary>Opens a collapsible category node.</summary>
    private static void AppendCategoryStart(StringBuilder sb, string label, bool expanded)
    {
        string collapsed = expanded ? "" : " is-collapsed";
        sb.AppendLine($"                                <li class=\"sidebar__cat-section\">");
        sb.AppendLine($"                                    <button class=\"sidebar__cat-toggle{collapsed}\">{HtmlEncode(label)}</button>");
        sb.AppendLine($"                                    <ul class=\"sidebar__cat-body{collapsed}\">");
    }

    /// <summary>Closes a collapsible category node.</summary>
    private static void AppendCategoryEnd(StringBuilder sb)
    {
        sb.AppendLine("                                    </ul>");
        sb.AppendLine("                                </li>");
    }

    /// <summary>Returns <c>true</c> when the current member page belongs to this category.</summary>
    private static bool IsCategoryActive(
        List<MemberInfo> members, string nsSlug, string typeSlug, string? currentMemberFileBase)
    {
        if (currentMemberFileBase is null) return false;
        return members
            .Select(m => MarkdownGenerator.GetMemberPageFileBase(nsSlug, typeSlug, MarkdownGenerator.MemberToSlug(m.GroupKey)))
            .Distinct()
            .Any(fb => fb == currentMemberFileBase);
    }

    private static string GetOperatorGroupDisplayName(string clrName) => clrName switch
    {
        "op_Implicit" => "Implicit",
        "op_Explicit" => "Explicit",
        _ => clrName.Replace("op_", ""),
    };

    /// <summary>
    /// Builds the sidebar as a self-contained HTML string (for embedding in Razor views).
    /// </summary>
    public static string Build(
        Dictionary<string, NamespaceInfo> namespaces,
        string baseUrl,
        string? currentNsSlug = null,
        string? currentTypeFileBase = null,
        string? currentMemberFileBase = null)
    {
        StringBuilder sb = new();
        AppendSidebar(sb, namespaces, currentNsSlug, currentTypeFileBase, baseUrl, currentMemberFileBase);
        return sb.ToString();
    }

    private static string HtmlEncode(string text) => HttpUtility.HtmlEncode(text);

    /// <summary>
    /// HTML-encodes the text and inserts <c>&lt;wbr&gt;</c> word-break opportunities
    /// at PascalCase boundaries (e.g. before an uppercase letter that follows a
    /// lowercase letter) and after dots, so the browser can wrap long type names
    /// like <c>JsonElement.ObjectBuilder</c> within the narrow sidebar.
    /// </summary>
    private static string HtmlEncodeWithBreaks(string text)
    {
        string encoded = HttpUtility.HtmlEncode(text);
        StringBuilder result = new(encoded.Length + encoded.Length / 4);

        for (int i = 0; i < encoded.Length; i++)
        {
            char c = encoded[i];

            // After a dot, insert a break opportunity
            if (c == '.' && i + 1 < encoded.Length)
            {
                result.Append(c);
                result.Append("<wbr>");
                continue;
            }

            // Before an uppercase letter that follows a lowercase letter
            if (i > 0 && char.IsUpper(c) && char.IsLower(encoded[i - 1]))
            {
                result.Append("<wbr>");
            }

            result.Append(c);
        }

        return result.ToString();
    }
}
