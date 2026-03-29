using System.Reflection;

namespace XmlDocToMarkdown;

/// <summary>
/// Information about a namespace and all its public types.
/// </summary>
public sealed class NamespaceInfo
{
    public string Name { get; set; } = string.Empty;
    public List<TypeInfo> Types { get; set; } = [];
}

/// <summary>
/// Information about a public type.
/// </summary>
public sealed class TypeInfo
{
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty; // class, struct, interface, enum, delegate
    public string? BaseType { get; set; }
    public string? BaseTypeFullName { get; set; }
    public List<string> Interfaces { get; set; } = [];
    public List<(string DisplayName, string? FullName)> InterfacesWithFullNames { get; set; } = [];
    public List<(string DisplayName, string? FullName)> ImplementedBy { get; set; } = [];
    public List<string> GenericParameters { get; set; } = [];
    public List<GenericConstraintInfo> GenericConstraints { get; set; } = [];
    public DocMember? Documentation { get; set; }
    public List<MemberInfo> Constructors { get; set; } = [];
    public List<MemberInfo> Properties { get; set; } = [];
    public List<MemberInfo> Methods { get; set; } = [];
    public List<MemberInfo> Operators { get; set; } = [];
    public List<MemberInfo> Fields { get; set; } = [];
    public List<MemberInfo> Events { get; set; } = [];
    public List<TypeInfo> NestedTypes { get; set; } = [];
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public bool AvailableOnNetStandard20 { get; set; } = true;
}

/// <summary>
/// Information about a type member (method, property, field, event, or constructor).
/// </summary>
public sealed class MemberInfo
{
    public string Name { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public string? ReturnTypeFullName { get; set; }
    public List<ParameterInfo> Parameters { get; set; } = [];
    public DocMember? Documentation { get; set; }
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsOverride { get; set; }
    public string XmlDocKey { get; set; } = string.Empty;

    /// <summary>
    /// Interface methods that this member implements.
    /// </summary>
    public List<ImplementsInfo> Implements { get; set; } = [];

    /// <summary>
    /// If this member overrides a base type member, the declaring type of the base definition.
    /// </summary>
    public string? OverridesType { get; set; }

    /// <summary>
    /// Full name of the overrides type for link resolution.
    /// </summary>
    public string? OverridesTypeFullName { get; set; }

    /// <summary>
    /// Key used to group overloads on the same member detail page.
    /// For methods: the base method name (no generic arity).
    /// For operators: the CLR method name (e.g. "op_Implicit").
    /// For constructors: ".ctor".
    /// For properties/fields/events: same as <see cref="Name"/>.
    /// </summary>
    public string GroupKey { get; set; } = string.Empty;
    public bool AvailableOnNetStandard20 { get; set; } = true;
}

public sealed class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? TypeFullName { get; set; }
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Describes an interface method that a member implements.
/// </summary>
public sealed class ImplementsInfo
{
    /// <summary>Display name of the interface (e.g. "IEquatable&lt;JsonElement&gt;").</summary>
    public string InterfaceDisplayName { get; set; } = string.Empty;

    /// <summary>Full CLR name used for link resolution.</summary>
    public string? InterfaceFullName { get; set; }

    /// <summary>Name of the member on the interface.</summary>
    public string MemberName { get; set; } = string.Empty;
}

/// <summary>
/// Generic constraint information for a type parameter.
/// </summary>
public sealed class GenericConstraintInfo
{
    public string ParameterName { get; set; } = string.Empty;
    public List<string> Constraints { get; set; } = [];
}

/// <summary>
/// Uses <see cref="MetadataLoadContext"/> to inspect public types from a compiled assembly
/// without loading it into the execution context.
/// </summary>
public sealed class AssemblyInspector(string assemblyPath)
{
    /// <summary>
    /// Quick pre-scan: returns a map of type FullName → per-type page URL slug.
    /// Used to populate <see cref="XmlDocParser.TypeUrlMap"/> before XML parsing.
    /// </summary>
    public Dictionary<string, string> PreScanTypeUrls(string baseUrl)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string assemblyDir = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;

        PathAssemblyResolver resolver = new(GetAssemblyPaths(runtimeDir, assemblyDir));
        using MetadataLoadContext mlc = new(resolver, coreAssemblyName: "System.Runtime");
        Assembly assembly = mlc.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));

        foreach (Type type in assembly.GetExportedTypes())
        {
            if (type.Name.StartsWith('<') || type.Name.Contains('$') || type.FullName is null)
            {
                continue;
            }

            string ns = type.Namespace ?? "(global)";
            string nsSlug = MarkdownGenerator.NamespaceToFileName(ns);
            string typeName = FormatTypeName(type);
            string typeSlug = MarkdownGenerator.TypeToSlug(typeName);
            string url = $"{baseUrl}/{nsSlug}-{typeSlug}.html";

            // Map by FullName (uses '+' for nested types, e.g. "Corvus.Text.Json.JsonElement+Source")
            map[type.FullName] = url;

            // Also map by dot-separated form used in XML docs (e.g. "Corvus.Text.Json.JsonElement.Source")
            if (type.IsNested && type.FullName.Contains('+'))
            {
                map[type.FullName.Replace('+', '.')] = url;
            }
        }

        return map;
    }

    /// <summary>
    /// Scans an assembly and returns the set of type FullNames and member XmlDocKeys
    /// present in it. Used to determine TFM-specific availability.
    /// </summary>
    public static HashSet<string> ScanMemberKeys(string scanAssemblyPath)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);

        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string scanDir = Path.GetDirectoryName(Path.GetFullPath(scanAssemblyPath))!;

        PathAssemblyResolver resolver = new(GetAssemblyPaths(runtimeDir, scanDir));
        using MetadataLoadContext mlc = new(resolver, coreAssemblyName: "System.Runtime");
        Assembly assembly = mlc.LoadFromAssemblyPath(Path.GetFullPath(scanAssemblyPath));

        foreach (Type type in assembly.GetExportedTypes())
        {
            if (type.Name.StartsWith('<') || type.Name.Contains('$') || type.FullName is null)
            {
                continue;
            }

            // Add type key
            keys.Add($"T:{type.FullName}");

            // Add member keys
            foreach (System.Reflection.ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                keys.Add(BuildConstructorXmlKey(type, ctor));
            }

            foreach (System.Reflection.PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                string fullName = type.FullName;
                System.Reflection.ParameterInfo[] indexParams = prop.GetIndexParameters();
                if (indexParams.Length > 0)
                {
                    string paramTypes = string.Join(",", indexParams.Select(p => GetXmlDocTypeName(p.ParameterType)));
                    keys.Add($"P:{fullName}.{prop.Name}({paramTypes})");
                }
                else
                {
                    keys.Add($"P:{fullName}.{prop.Name}");
                }
            }

            foreach (System.Reflection.MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal))
                {
                    keys.Add(BuildOperatorXmlKey(type, method));
                }
                else if (!method.IsSpecialName)
                {
                    keys.Add(BuildMethodXmlKey(type, method));
                }
            }

            foreach (System.Reflection.FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!field.IsSpecialName)
                {
                    keys.Add($"F:{type.FullName}.{field.Name}");
                }
            }

            foreach (System.Reflection.EventInfo evt in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                keys.Add($"E:{type.FullName}.{evt.Name}");
            }
        }

        return keys;
    }

    public Dictionary<string, NamespaceInfo> Inspect(Dictionary<string, DocMember> xmlDocs)
    {
        Dictionary<string, NamespaceInfo> namespaces = new(StringComparer.Ordinal);

        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string assemblyDir = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;

        PathAssemblyResolver resolver = new(GetAssemblyPaths(runtimeDir, assemblyDir));
        using MetadataLoadContext mlc = new(resolver, coreAssemblyName: "System.Runtime");
        Assembly assembly = mlc.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));

        foreach (Type type in assembly.GetExportedTypes())
        {
            // Skip compiler-generated types
            if (type.Name.StartsWith('<') || type.Name.Contains('$') || type.FullName is null)
            {
                continue;
            }

            string ns = type.Namespace ?? "(global)";

            // For nested types, we attach them to the parent type later
            if (type.IsNested)
            {
                continue;
            }

            if (!namespaces.TryGetValue(ns, out NamespaceInfo? nsInfo))
            {
                nsInfo = new NamespaceInfo { Name = ns };
                namespaces[ns] = nsInfo;
            }

            TypeInfo typeInfo;
            try
            {
                typeInfo = BuildTypeInfo(type, xmlDocs, mlc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Warning: Skipping type {type.FullName}: {ex.Message}");
                continue;
            }

            nsInfo.Types.Add(typeInfo);
        }

        // Sort types within each namespace, then flatten nested types as peers
        foreach (NamespaceInfo ns in namespaces.Values)
        {
            List<TypeInfo> flattened = [];
            foreach (TypeInfo type in ns.Types)
            {
                flattened.Add(type);
                FlattenNestedTypes(flattened, type);
                type.NestedTypes.Clear();
            }

            ns.Types = flattened;
            ns.Types.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }

        return namespaces;
    }

    /// <summary>
    /// Recursively collects all nested types into a flat list, then clears
    /// each parent's NestedTypes so they are only represented as peers.
    /// </summary>
    private static void FlattenNestedTypes(List<TypeInfo> target, TypeInfo parent)
    {
        foreach (TypeInfo nested in parent.NestedTypes)
        {
            target.Add(nested);
            FlattenNestedTypes(target, nested);
            nested.NestedTypes.Clear();
        }
    }

    private static TypeInfo BuildTypeInfo(Type type, Dictionary<string, DocMember> xmlDocs, MetadataLoadContext mlc)
    {
        string fullName = type.FullName ?? type.Name;
        string xmlKey = $"T:{fullName}";

        TypeInfo typeInfo = new()
        {
            FullName = fullName,
            Name = FormatTypeName(type),
            Namespace = type.Namespace ?? "(global)",
            Kind = GetTypeKind(type),
            BaseType = GetBaseTypeName(type),
            BaseTypeFullName = GetBaseTypeFullName(type),
            IsStatic = type.IsAbstract && type.IsSealed,
            IsAbstract = type.IsAbstract && !type.IsSealed && !type.IsInterface,
            IsSealed = type.IsSealed && !type.IsAbstract,
        };

        xmlDocs.TryGetValue(xmlKey, out DocMember? typeDocs);
        typeInfo.Documentation = typeDocs;

        // Generic parameters and constraints
        if (type.IsGenericType)
        {
            Type[] genericArgs = type.GetGenericArguments();
            foreach (Type gp in genericArgs)
            {
                typeInfo.GenericParameters.Add(gp.Name);
            }

            typeInfo.GenericConstraints = ExtractGenericConstraints(genericArgs);
        }

        // Interfaces
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.IsPublic || iface.IsNestedPublic)
            {
                typeInfo.Interfaces.Add(FormatTypeName(iface));
                typeInfo.InterfacesWithFullNames.Add((FormatTypeName(iface), GetTypeFullName(iface)));
            }
        }

        // Constructors
        foreach (System.Reflection.ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            string ctorXmlKey = BuildConstructorXmlKey(type, ctor);
            xmlDocs.TryGetValue(ctorXmlKey, out DocMember? ctorDocs);
            typeInfo.Constructors.Add(BuildConstructorMemberInfo(type, ctor, ctorDocs, ctorXmlKey));
        }

        // Properties
        foreach (System.Reflection.PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            System.Reflection.ParameterInfo[] indexParams = prop.GetIndexParameters();
            string propXmlKey;
            if (indexParams.Length > 0)
            {
                string paramTypes = string.Join(",", indexParams.Select(p => GetXmlDocTypeName(p.ParameterType)));
                propXmlKey = $"P:{fullName}.{prop.Name}({paramTypes})";
            }
            else
            {
                propXmlKey = $"P:{fullName}.{prop.Name}";
            }

            xmlDocs.TryGetValue(propXmlKey, out DocMember? propDocs);
            typeInfo.Properties.Add(BuildPropertyMemberInfo(prop, propDocs, propXmlKey));
        }

        // Methods and operators (excluding property accessors and event accessors)
        foreach (System.Reflection.MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName)
            {
                if (method.Name.StartsWith("op_", StringComparison.Ordinal))
                {
                    string opXmlKey = BuildOperatorXmlKey(type, method);
                    xmlDocs.TryGetValue(opXmlKey, out DocMember? opDocs);
                    typeInfo.Operators.Add(BuildOperatorMemberInfo(method, opDocs, opXmlKey));
                }

                continue;
            }

            string methodXmlKey = BuildMethodXmlKey(type, method);
            xmlDocs.TryGetValue(methodXmlKey, out DocMember? methodDocs);
            typeInfo.Methods.Add(BuildMethodMemberInfo(method, methodDocs, methodXmlKey));
        }

        // Fields (for enums and public fields)
        foreach (System.Reflection.FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (field.IsSpecialName)
            {
                continue;
            }

            string fieldXmlKey = $"F:{fullName}.{field.Name}";
            xmlDocs.TryGetValue(fieldXmlKey, out DocMember? fieldDocs);
            typeInfo.Fields.Add(new MemberInfo
            {
                Name = field.Name,
                GroupKey = field.Name,
                Signature = $"{FormatTypeName(field.FieldType)} {field.Name}",
                ReturnType = FormatTypeName(field.FieldType),
                ReturnTypeFullName = GetTypeFullName(field.FieldType),
                Documentation = fieldDocs,
                IsStatic = field.IsStatic,
                XmlDocKey = fieldXmlKey,
            });
        }

        // Events
        foreach (System.Reflection.EventInfo evt in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            string evtXmlKey = $"E:{fullName}.{evt.Name}";
            xmlDocs.TryGetValue(evtXmlKey, out DocMember? evtDocs);
            typeInfo.Events.Add(new MemberInfo
            {
                Name = evt.Name ?? string.Empty,
                GroupKey = evt.Name ?? string.Empty,
                Signature = $"event {FormatTypeName(evt.EventHandlerType!)} {evt.Name}",
                ReturnType = FormatTypeName(evt.EventHandlerType!),
                ReturnTypeFullName = GetTypeFullName(evt.EventHandlerType!),
                Documentation = evtDocs,
                XmlDocKey = evtXmlKey,
            });
        }

        // Nested types (skip compiler-generated types with '<' or '$' in the name)
        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
        {
            if (nested.Name.Contains('<') || nested.Name.Contains('$'))
            {
                continue;
            }

            typeInfo.NestedTypes.Add(BuildTypeInfo(nested, xmlDocs, mlc));
        }

        typeInfo.NestedTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        return typeInfo;
    }

    private static MemberInfo BuildConstructorMemberInfo(Type type, System.Reflection.ConstructorInfo ctor, DocMember? docs, string xmlKey)
    {
        System.Reflection.ParameterInfo[] parameters = ctor.GetParameters();
        string paramList = string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));
        string shortName = FormatTypeName(type);
        // Remove generic suffix for constructor name
        int backtick = shortName.IndexOf('<');
        if (backtick >= 0)
        {
            shortName = shortName[..backtick];
        }

        return new MemberInfo
        {
            Name = shortName,
            GroupKey = ".ctor",
            Signature = $"public {shortName}({paramList})",
            Parameters = parameters.Select(p => new ParameterInfo
            {
                Name = p.Name ?? string.Empty,
                Type = FormatTypeName(p.ParameterType),
                TypeFullName = GetTypeFullName(p.ParameterType),
                IsOptional = p.IsOptional,
                DefaultValue = p.HasDefaultValue ? p.RawDefaultValue?.ToString() : null,
            }).ToList(),
            Documentation = docs,
            XmlDocKey = xmlKey,
        };
    }

    private static MemberInfo BuildPropertyMemberInfo(System.Reflection.PropertyInfo prop, DocMember? docs, string xmlKey)
    {
        string accessors = "";
        if (prop.CanRead && prop.CanWrite)
        {
            accessors = " { get; set; }";
        }
        else if (prop.CanRead)
        {
            accessors = " { get; }";
        }
        else if (prop.CanWrite)
        {
            accessors = " { set; }";
        }

        System.Reflection.ParameterInfo[] indexParams = prop.GetIndexParameters();
        bool isIndexer = indexParams.Length > 0;

        // Build display name: "this[int]" for indexers, "Name" for regular props
        string displayName = prop.Name;
        string signature;
        var parameters = new List<ParameterInfo>();

        string modifiers = BuildPropertyModifiers(prop);

        if (isIndexer)
        {
            string paramList = string.Join(", ", indexParams.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));
            displayName = $"this[{string.Join(", ", indexParams.Select(p => FormatTypeName(p.ParameterType)))}]";
            signature = $"{modifiers} {FormatTypeName(prop.PropertyType)} this[{paramList}]{accessors}";

            foreach (System.Reflection.ParameterInfo p in indexParams)
            {
                parameters.Add(new ParameterInfo
                {
                    Name = p.Name ?? "index",
                    Type = FormatTypeName(p.ParameterType),
                    TypeFullName = GetTypeFullName(p.ParameterType),
                    IsOptional = p.IsOptional,
                });
            }
        }
        else
        {
            signature = $"{modifiers} {FormatTypeName(prop.PropertyType)} {prop.Name}{accessors}";
        }

        System.Reflection.MethodInfo? getter = prop.GetGetMethod();
        Type? declaringType = prop.DeclaringType;

        // Detect overrides and interface implementations
        Type? baseDefType = FindPropertyBaseDefinitionType(prop);
        List<ImplementsInfo> implements = [];
        if (declaringType is not null && !(getter?.IsStatic ?? false))
        {
            implements = FindPropertyInterfaceImplementations(prop, declaringType);
        }

        return new MemberInfo
        {
            Name = displayName,
            GroupKey = prop.Name,
            Signature = signature,
            ReturnType = FormatTypeName(prop.PropertyType),
            ReturnTypeFullName = GetTypeFullName(prop.PropertyType),
            Documentation = docs,
            IsStatic = getter?.IsStatic ?? false,
            IsOverride = baseDefType is not null,
            OverridesType = baseDefType is not null ? FormatTypeName(baseDefType) : null,
            OverridesTypeFullName = baseDefType is not null ? GetTypeFullName(baseDefType) : null,
            Implements = implements,
            XmlDocKey = xmlKey,
            Parameters = parameters,
        };
    }

    private static MemberInfo BuildMethodMemberInfo(System.Reflection.MethodInfo method, DocMember? docs, string xmlKey)
    {
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();
        string paramList = string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

        string genericSuffix = "";
        string genericConstraints = "";
        if (method.IsGenericMethod)
        {
            Type[] genericArgs = method.GetGenericArguments();
            genericSuffix = $"<{string.Join(", ", genericArgs.Select(g => g.Name))}>";
            List<GenericConstraintInfo> constraints = ExtractGenericConstraints(genericArgs);
            if (constraints.Count > 0)
            {
                genericConstraints = string.Join("", constraints.Select(c =>
                    $"\n    where {c.ParameterName} : {string.Join(", ", c.Constraints)}"));
            }
        }

        // Walk the base type chain to detect overrides (GetBaseDefinition() is not
        // supported by MetadataLoadContext).
        Type? baseDefType = FindBaseDefinitionType(method);
        bool isOverride = baseDefType is not null;

        string modifiers = BuildMethodModifiers(method, isOverride);

        // Find interface implementations
        List<ImplementsInfo> implements = [];
        if (method.DeclaringType is not null && !method.IsStatic)
        {
            implements = FindInterfaceImplementations(method, method.DeclaringType);
        }

        return new MemberInfo
        {
            Name = method.Name,
            GroupKey = method.Name,
            Signature = $"{modifiers} {FormatTypeName(method.ReturnType)} {method.Name}{genericSuffix}({paramList}){genericConstraints}",
            ReturnType = FormatTypeName(method.ReturnType),
            ReturnTypeFullName = GetTypeFullName(method.ReturnType),
            Parameters = parameters.Select(p => new ParameterInfo
            {
                Name = p.Name ?? string.Empty,
                Type = FormatTypeName(p.ParameterType),
                TypeFullName = GetTypeFullName(p.ParameterType),
                IsOptional = p.IsOptional,
                DefaultValue = p.HasDefaultValue ? p.RawDefaultValue?.ToString() : null,
            }).ToList(),
            Documentation = docs,
            IsStatic = method.IsStatic,
            IsVirtual = method.IsVirtual && !method.IsFinal,
            IsAbstract = method.IsAbstract,
            IsOverride = isOverride,
            OverridesType = baseDefType is not null ? FormatTypeName(baseDefType) : null,
            OverridesTypeFullName = baseDefType is not null ? GetTypeFullName(baseDefType) : null,
            Implements = implements,
            XmlDocKey = xmlKey,
        };
    }

    private static string BuildConstructorXmlKey(Type type, System.Reflection.ConstructorInfo ctor)
    {
        string fullName = type.FullName ?? type.Name;
        System.Reflection.ParameterInfo[] parameters = ctor.GetParameters();
        if (parameters.Length == 0)
        {
            return $"M:{fullName}.#ctor";
        }

        string paramTypes = string.Join(",", parameters.Select(p => GetXmlDocTypeName(p.ParameterType)));
        return $"M:{fullName}.#ctor({paramTypes})";
    }

    private static string BuildMethodXmlKey(Type type, System.Reflection.MethodInfo method)
    {
        string fullName = type.FullName ?? type.Name;
        string methodName = method.Name;

        if (method.IsGenericMethod)
        {
            methodName += $"``{method.GetGenericArguments().Length}";
        }

        System.Reflection.ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return $"M:{fullName}.{methodName}";
        }

        string paramTypes = string.Join(",", parameters.Select(p => GetXmlDocTypeName(p.ParameterType)));
        return $"M:{fullName}.{methodName}({paramTypes})";
    }

    private static string BuildOperatorXmlKey(Type type, System.Reflection.MethodInfo method)
    {
        string fullName = type.FullName ?? type.Name;
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();
        string paramTypes = string.Join(",", parameters.Select(p => GetXmlDocTypeName(p.ParameterType)));
        string key = $"M:{fullName}.{method.Name}({paramTypes})";

        // Conversion operators (op_Implicit, op_Explicit) include ~ReturnType in the XML doc key
        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            key += $"~{GetXmlDocTypeName(method.ReturnType)}";
        }

        return key;
    }

    private static MemberInfo BuildOperatorMemberInfo(System.Reflection.MethodInfo method, DocMember? docs, string xmlKey)
    {
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();
        string displayName = GetOperatorDisplayName(method);
        string paramList = string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));
        string signature = method.Name is "op_Implicit" or "op_Explicit"
            ? $"public static {displayName}({paramList})"
            : $"public static {FormatTypeName(method.ReturnType)} {displayName}({paramList})";

        return new MemberInfo
        {
            Name = displayName,
            GroupKey = method.Name,
            Signature = signature,
            ReturnType = FormatTypeName(method.ReturnType),
            ReturnTypeFullName = GetTypeFullName(method.ReturnType),
            Parameters = parameters.Select(p => new ParameterInfo
            {
                Name = p.Name ?? string.Empty,
                Type = FormatTypeName(p.ParameterType),
                TypeFullName = GetTypeFullName(p.ParameterType),
            }).ToList(),
            Documentation = docs,
            IsStatic = true,
            XmlDocKey = xmlKey,
        };
    }

    /// <summary>
    /// Checks whether two methods have matching parameter type signatures.
    /// </summary>
    private static bool ParameterTypesMatch(System.Reflection.ParameterInfo[] a, System.Reflection.ParameterInfo[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].ParameterType.FullName != b[i].ParameterType.FullName)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Walks the base type chain to find the declaring type of the original virtual method.
    /// Returns null if no base definition was found (i.e. the method is not an override).
    /// </summary>
    private static Type? FindBaseDefinitionType(System.Reflection.MethodInfo method)
    {
        if (method.IsStatic || !method.IsVirtual)
        {
            return null;
        }

        Type? declaringType = method.DeclaringType;
        if (declaringType is null)
        {
            return null;
        }

        System.Reflection.ParameterInfo[] methodParams = method.GetParameters();
        Type? baseType = declaringType.BaseType;

        while (baseType is not null)
        {
            System.Reflection.MethodInfo[] baseMethods = baseType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (System.Reflection.MethodInfo baseMethod in baseMethods)
            {
                if (baseMethod.Name == method.Name
                    && baseMethod.IsVirtual
                    && ParameterTypesMatch(baseMethod.GetParameters(), methodParams))
                {
                    return baseType;
                }
            }

            baseType = baseType.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Walks the base type chain to find the declaring type of the original virtual property.
    /// </summary>
    private static Type? FindPropertyBaseDefinitionType(System.Reflection.PropertyInfo prop)
    {
        System.Reflection.MethodInfo? accessor = prop.GetGetMethod() ?? prop.GetSetMethod();
        if (accessor is null)
        {
            return null;
        }

        return FindBaseDefinitionType(accessor);
    }

    /// <summary>
    /// Finds interfaces whose methods match the given method by name and parameter types.
    /// </summary>
    private static List<ImplementsInfo> FindInterfaceImplementations(
        System.Reflection.MethodInfo method, Type declaringType)
    {
        List<ImplementsInfo> result = [];
        System.Reflection.ParameterInfo[] methodParams = method.GetParameters();

        foreach (Type iface in declaringType.GetInterfaces())
        {
            foreach (System.Reflection.MethodInfo ifaceMethod in iface.GetMethods())
            {
                if (ifaceMethod.Name == method.Name
                    && ParameterTypesMatch(ifaceMethod.GetParameters(), methodParams))
                {
                    result.Add(new ImplementsInfo
                    {
                        InterfaceDisplayName = FormatTypeName(iface),
                        InterfaceFullName = GetTypeFullName(iface),
                        MemberName = ifaceMethod.Name,
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finds interfaces whose properties match the given property by name.
    /// </summary>
    private static List<ImplementsInfo> FindPropertyInterfaceImplementations(
        System.Reflection.PropertyInfo prop, Type declaringType)
    {
        List<ImplementsInfo> result = [];

        foreach (Type iface in declaringType.GetInterfaces())
        {
            System.Reflection.PropertyInfo? ifaceProp = iface.GetProperty(
                prop.Name, BindingFlags.Public | BindingFlags.Instance);

            if (ifaceProp is not null
                && ifaceProp.PropertyType.FullName == prop.PropertyType.FullName)
            {
                result.Add(new ImplementsInfo
                {
                    InterfaceDisplayName = FormatTypeName(iface),
                    InterfaceFullName = GetTypeFullName(iface),
                    MemberName = prop.Name,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the C# modifier prefix for a method (public, static, override, virtual, abstract).
    /// </summary>
    private static string BuildMethodModifiers(System.Reflection.MethodInfo method, bool isOverride)
    {
        var parts = new List<string> { "public" };
        if (method.IsStatic)
        {
            parts.Add("static");
        }
        else if (isOverride)
        {
            parts.Add("override");
        }
        else if (method.IsAbstract)
        {
            parts.Add("abstract");
        }
        else if (method.IsVirtual && !method.IsFinal)
        {
            parts.Add("virtual");
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Builds the C# modifier prefix for a property.
    /// </summary>
    private static string BuildPropertyModifiers(System.Reflection.PropertyInfo prop)
    {
        var parts = new List<string> { "public" };
        System.Reflection.MethodInfo? accessor = prop.GetGetMethod() ?? prop.GetSetMethod();
        if (accessor is not null)
        {
            if (accessor.IsStatic)
            {
                parts.Add("static");
            }
            else
            {
                bool isOverride = false;
                try
                {
                    isOverride = accessor.GetBaseDefinition().DeclaringType != accessor.DeclaringType;
                }
                catch (NotSupportedException) { }

                if (isOverride)
                {
                    parts.Add("override");
                }
                else if (accessor.IsAbstract)
                {
                    parts.Add("abstract");
                }
                else if (accessor.IsVirtual && !accessor.IsFinal)
                {
                    parts.Add("virtual");
                }
            }
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Converts a CLR operator method name to its C# display form.
    /// </summary>
    private static string GetOperatorDisplayName(System.Reflection.MethodInfo method)
    {
        return method.Name switch
        {
            "op_Implicit" => $"implicit operator {FormatTypeName(method.ReturnType)}",
            "op_Explicit" => $"explicit operator {FormatTypeName(method.ReturnType)}",
            "op_Addition" => "operator +",
            "op_Subtraction" => "operator -",
            "op_Multiply" or "op_Multiplication" => "operator *",
            "op_Division" => "operator /",
            "op_Modulus" => "operator %",
            "op_BitwiseAnd" => "operator &",
            "op_BitwiseOr" => "operator |",
            "op_ExclusiveOr" => "operator ^",
            "op_LeftShift" => "operator <<",
            "op_RightShift" => "operator >>",
            "op_UnsignedRightShift" => "operator >>>",
            "op_Equality" => "operator ==",
            "op_Inequality" => "operator !=",
            "op_LessThan" => "operator <",
            "op_GreaterThan" => "operator >",
            "op_LessThanOrEqual" => "operator <=",
            "op_GreaterThanOrEqual" => "operator >=",
            "op_UnaryPlus" => "operator +",
            "op_UnaryNegation" => "operator -",
            "op_LogicalNot" => "operator !",
            "op_OnesComplement" => "operator ~",
            "op_Increment" => "operator ++",
            "op_Decrement" => "operator --",
            "op_True" => "operator true",
            "op_False" => "operator false",
            _ => method.Name,
        };
    }

    private static string GetXmlDocTypeName(Type type)
    {
        if (type.IsGenericParameter)
        {
            // Method-level generic parameters use ``N, type-level use `N
            if (type.DeclaringMethod is not null)
            {
                return $"``{type.GenericParameterPosition}";
            }

            return $"`{type.GenericParameterPosition}";
        }

        if (type.IsArray)
        {
            return GetXmlDocTypeName(type.GetElementType()!) + "[]";
        }

        if (type.IsByRef)
        {
            return GetXmlDocTypeName(type.GetElementType()!) + "@";
        }

        if (type.IsGenericType)
        {
            string baseName = type.GetGenericTypeDefinition().FullName ?? type.Name;
            int backtick = baseName.IndexOf('`');
            if (backtick >= 0)
            {
                baseName = baseName[..backtick];
            }

            Type[] args = type.GetGenericArguments();
            return $"{baseName}{{{string.Join(",", args.Select(GetXmlDocTypeName))}}}";
        }

        return type.FullName ?? type.Name;
    }

    internal static string FormatTypeName(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type.IsArray)
        {
            return FormatTypeName(type.GetElementType()!) + "[]";
        }

        if (type.IsByRef)
        {
            return "ref " + FormatTypeName(type.GetElementType()!);
        }

        // Handle Nullable<T>
        if (type.IsGenericType)
        {
            Type genericDef = type.GetGenericTypeDefinition();
            string baseName = genericDef.Name;
            int backtick = baseName.IndexOf('`');
            if (backtick >= 0)
            {
                baseName = baseName[..backtick];
            }

            // Use the declaring type for nested types
            if (type.IsNested && type.DeclaringType is not null)
            {
                baseName = FormatTypeName(type.DeclaringType) + "." + baseName;
            }

            Type[] args = type.GetGenericArguments();
            return $"{baseName}<{string.Join(", ", args.Select(FormatTypeName))}>";
        }

        if (type.IsNested && type.DeclaringType is not null)
        {
            return FormatTypeName(type.DeclaringType) + "." + type.Name;
        }

        // Use C# keyword aliases for common types
        return type.FullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Char" => "char",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.String" => "string",
            "System.Object" => "object",
            _ => type.Name,
        };
    }

    private static string? GetBaseTypeName(Type type)
    {
        if (type.BaseType is null || type.BaseType.FullName == "System.Object" ||
            type.BaseType.FullName == "System.ValueType" || type.BaseType.FullName == "System.Enum")
        {
            return null;
        }

        return FormatTypeName(type.BaseType);
    }

    private static List<GenericConstraintInfo> ExtractGenericConstraints(Type[] genericArgs)
    {
        List<GenericConstraintInfo> constraints = [];

        foreach (Type gp in genericArgs)
        {
            if (!gp.IsGenericParameter)
            {
                continue;
            }

            List<string> parts = [];
            GenericParameterAttributes attrs = gp.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;

            // class/struct/unmanaged constraints
            if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                // Check for unmanaged constraint (struct + System.ValueType constraint type that is unmanaged)
                Type[] typeConstraints = gp.GetGenericParameterConstraints();
                bool isUnmanaged = typeConstraints.Any(c => c.FullName == "System.ValueType") &&
                                   (attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 &&
                                   gp.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsUnmanagedAttribute");
                parts.Add(isUnmanaged ? "unmanaged" : "struct");
            }
            else if ((attrs & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                parts.Add("class");
            }

            // Type constraints (base class, interfaces)
            foreach (Type constraint in gp.GetGenericParameterConstraints())
            {
                if (constraint.FullName == "System.ValueType")
                {
                    continue; // Already handled by struct/unmanaged
                }

                parts.Add(FormatTypeName(constraint));
            }

            // notnull constraint
            if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0 &&
                (attrs & GenericParameterAttributes.ReferenceTypeConstraint) == 0 &&
                gp.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute"))
            {
                // This may indicate notnull in some contexts, but it's complex to detect reliably
            }

            // new() constraint
            if ((attrs & GenericParameterAttributes.DefaultConstructorConstraint) != 0 &&
                (attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
            {
                parts.Add("new()");
            }

            if (parts.Count > 0)
            {
                constraints.Add(new GenericConstraintInfo
                {
                    ParameterName = gp.Name,
                    Constraints = parts,
                });
            }
        }

        return constraints;
    }

    /// <summary>
    /// Gets the full type name suitable for URL resolution (e.g., System.String, System.Collections.Generic.List`1).
    /// Returns null for generic parameter types.
    /// </summary>
    internal static string? GetTypeFullName(Type type)
    {
        if (type.IsGenericParameter)
        {
            return null;
        }

        if (type.IsArray)
        {
            return GetTypeFullName(type.GetElementType()!);
        }

        if (type.IsByRef)
        {
            return GetTypeFullName(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            Type genericDef = type.GetGenericTypeDefinition();
            return genericDef.FullName;
        }

        return type.FullName;
    }

    private static string? GetBaseTypeFullName(Type type)
    {
        if (type.BaseType is null || type.BaseType.FullName == "System.Object" ||
            type.BaseType.FullName == "System.ValueType" || type.BaseType.FullName == "System.Enum")
        {
            return null;
        }

        return GetTypeFullName(type.BaseType);
    }

    private static string GetTypeKind(Type type)
    {
        if (type.IsEnum)
        {
            return "enum";
        }

        if (type.IsInterface)
        {
            return "interface";
        }

        if (type.IsValueType)
        {
            return "struct";
        }

        if (type.BaseType?.FullName == "System.MulticastDelegate")
        {
            return "delegate";
        }

        return "class";
    }

    private static IEnumerable<string> GetAssemblyPaths(string runtimeDir, string assemblyDir)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string dll in Directory.GetFiles(runtimeDir, "*.dll"))
        {
            string name = Path.GetFileName(dll);
            if (seen.Add(name))
            {
                yield return dll;
            }
        }

        // Also include DLLs from the assembly directory (for dependencies)
        foreach (string dll in Directory.GetFiles(assemblyDir, "*.dll"))
        {
            string name = Path.GetFileName(dll);
            if (seen.Add(name))
            {
                yield return dll;
            }
        }

        // Resolve NuGet package assemblies from the .deps.json file
        string depsJsonPath = Path.Combine(assemblyDir, Path.GetFileNameWithoutExtension(
            Directory.GetFiles(assemblyDir, "*.deps.json").FirstOrDefault() ?? string.Empty));
        string[] depsFiles = Directory.GetFiles(assemblyDir, "*.deps.json");
        if (depsFiles.Length > 0)
        {
            foreach (string nugetDll in ResolveNuGetAssemblies(depsFiles[0]))
            {
                string name = Path.GetFileName(nugetDll);
                if (seen.Add(name))
                {
                    yield return nugetDll;
                }
            }
        }
    }

    private static IEnumerable<string> ResolveNuGetAssemblies(string depsJsonPath)
    {
        // Parse the deps.json to find NuGet package references and resolve them
        // from the global NuGet packages cache.
        string nugetCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        if (!File.Exists(depsJsonPath) || !Directory.Exists(nugetCache))
        {
            yield break;
        }

        string json = File.ReadAllText(depsJsonPath);
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);

        // Get the target framework from runtimeTarget
        string? targetFramework = null;
        if (doc.RootElement.TryGetProperty("runtimeTarget", out System.Text.Json.JsonElement runtimeTarget))
        {
            targetFramework = runtimeTarget.GetProperty("name").GetString();
        }

        if (targetFramework is null)
        {
            yield break;
        }

        // Iterate over the libraries to find NuGet packages (type == "package")
        if (!doc.RootElement.TryGetProperty("libraries", out System.Text.Json.JsonElement libraries))
        {
            yield break;
        }

        foreach (System.Text.Json.JsonProperty lib in libraries.EnumerateObject())
        {
            if (!lib.Value.TryGetProperty("type", out System.Text.Json.JsonElement typeElement) ||
                typeElement.GetString() != "package")
            {
                continue;
            }

            // Library key is "PackageName/Version"
            string[] parts = lib.Name.Split('/');
            if (parts.Length != 2)
            {
                continue;
            }

            string packageId = parts[0].ToLowerInvariant();
            string version = parts[1];

            // Look for DLLs in the package directory, preferring net10.0 > net9.0 > net8.0 > netstandard2.1 > netstandard2.0
            string packageDir = Path.Combine(nugetCache, packageId, version, "lib");
            if (!Directory.Exists(packageDir))
            {
                continue;
            }

            string[] preferredTfms = ["net10.0", "net9.0", "net8.0", "net7.0", "net6.0", "netstandard2.1", "netstandard2.0"];
            foreach (string tfm in preferredTfms)
            {
                string tfmDir = Path.Combine(packageDir, tfm);
                if (Directory.Exists(tfmDir))
                {
                    foreach (string dll in Directory.GetFiles(tfmDir, "*.dll"))
                    {
                        yield return dll;
                    }

                    break;
                }
            }
        }
    }
}
