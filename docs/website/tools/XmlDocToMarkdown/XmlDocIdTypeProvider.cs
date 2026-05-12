using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace XmlDocToMarkdown;

/// <summary>
/// Decodes PE metadata type signatures into XmlDocKey-style type strings
/// (matching the format used in XML documentation comment IDs).
/// <para>
/// See: https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/documentation-comments#d42-id-string-format
/// </para>
/// </summary>
internal sealed class XmlDocIdTypeProvider : ISignatureTypeProvider<string, object?>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "System.Boolean",
        PrimitiveTypeCode.Byte => "System.Byte",
        PrimitiveTypeCode.SByte => "System.SByte",
        PrimitiveTypeCode.Char => "System.Char",
        PrimitiveTypeCode.Int16 => "System.Int16",
        PrimitiveTypeCode.UInt16 => "System.UInt16",
        PrimitiveTypeCode.Int32 => "System.Int32",
        PrimitiveTypeCode.UInt32 => "System.UInt32",
        PrimitiveTypeCode.Int64 => "System.Int64",
        PrimitiveTypeCode.UInt64 => "System.UInt64",
        PrimitiveTypeCode.Single => "System.Single",
        PrimitiveTypeCode.Double => "System.Double",
        PrimitiveTypeCode.String => "System.String",
        PrimitiveTypeCode.Object => "System.Object",
        PrimitiveTypeCode.IntPtr => "System.IntPtr",
        PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
        PrimitiveTypeCode.Void => "System.Void",
        PrimitiveTypeCode.TypedReference => "System.TypedReference",
        _ => typeCode.ToString(),
    };

    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        TypeDefinition typeDef = r.GetTypeDefinition(handle);
        string name = r.GetString(typeDef.Name);
        string ns = typeDef.Namespace.IsNil ? "" : r.GetString(typeDef.Namespace);

        // Handle nested types
        if (!typeDef.GetDeclaringType().IsNil)
        {
            string parent = GetTypeFromDefinition(r, typeDef.GetDeclaringType(), 0);
            return $"{parent}.{StripArity(name)}";
        }

        string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        return StripArity(fullName);
    }

    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle handle, byte rawTypeKind)
    {
        TypeReference typeRef = r.GetTypeReference(handle);
        string name = r.GetString(typeRef.Name);
        string ns = typeRef.Namespace.IsNil ? "" : r.GetString(typeRef.Namespace);

        // Handle nested types via ResolutionScope
        if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            string parent = GetTypeFromReference(r, (TypeReferenceHandle)typeRef.ResolutionScope, 0);
            return $"{parent}.{StripArity(name)}";
        }

        string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        return StripArity(fullName);
    }

    public string GetTypeFromSpecification(MetadataReader r, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        TypeSpecification spec = r.GetTypeSpecification(handle);
        return spec.DecodeSignature(this, genericContext);
    }

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        return $"{genericType}{{{string.Join(",", typeArguments)}}}";
    }

    public string GetGenericMethodParameter(object? genericContext, int index) => $"``{index}";

    public string GetGenericTypeParameter(object? genericContext, int index) => $"`{index}";

    public string GetArrayType(string elementType, ArrayShape shape)
    {
        if (shape.Rank == 1)
        {
            return $"{elementType}[]";
        }

        // Multi-dimensional: T[0:,0:] format
        string dims = string.Join(",", Enumerable.Range(0, shape.Rank).Select(i =>
        {
            int lower = i < shape.LowerBounds.Length ? shape.LowerBounds[i] : 0;
            int size = i < shape.Sizes.Length ? shape.Sizes[i] : 0;
            return size > 0 ? $"{lower}:{lower + size}" : $"{lower}:";
        }));
        return $"{elementType}[{dims}]";
    }

    public string GetSZArrayType(string elementType) => $"{elementType}[]";

    public string GetByReferenceType(string elementType) => $"{elementType}@";

    public string GetPointerType(string elementType) => $"{elementType}*";

    public string GetPinnedType(string elementType) => elementType;

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetFunctionPointerType(MethodSignature<string> signature) => "System.IntPtr";

    /// <summary>
    /// Strips the generic arity suffix from a type name (e.g. "List`1" → "List").
    /// XmlDocKey format uses {T} for generic args, not backtick arity.
    /// </summary>
    private static string StripArity(string name)
    {
        int backtick = name.LastIndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }

    /// <summary>
    /// Formats a method's parameters into the XmlDocKey parameter string.
    /// Returns the full key suffix including parens, e.g. "(System.String,System.Int32@)".
    /// Returns empty string if no parameters.
    /// </summary>
    public static string FormatMethodParams(MetadataReader peReader, MethodDefinition methodDef)
    {
        var provider = new XmlDocIdTypeProvider();
        MethodSignature<string> sig = methodDef.DecodeSignature(provider, null);

        if (sig.ParameterTypes.Length == 0)
        {
            return "";
        }

        return $"({string.Join(",", sig.ParameterTypes)})";
    }
}
