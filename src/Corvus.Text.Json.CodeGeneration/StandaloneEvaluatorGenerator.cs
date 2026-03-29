// <copyright file="StandaloneEvaluatorGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.Keywords;
using Corvus.Text.Json.CodeGeneration.Internal;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Generates a standalone static evaluator class that performs full JSON Schema
/// validation and annotation collection, independent of generated types.
/// This partial class handles the structural aspects: file header, class declaration,
/// entry points, and schema tree walking. Keyword-specific emission is handled
/// by other partial class files.
/// </summary>
internal static partial class StandaloneEvaluatorGenerator
{
    /// <summary>
    /// Generates a standalone evaluator for the given root type declaration.
    /// </summary>
    /// <param name="rootType">The root type declaration (before reduction).</param>
    /// <param name="options">The code generation options.</param>
    /// <param name="lineEnd">The line ending sequence.</param>
    /// <returns>A generated code file containing the evaluator, or <see langword="null"/> if generation is not applicable.</returns>
    public static GeneratedCodeFile? Generate(
        TypeDeclaration rootType,
        CSharpLanguageProvider.Options options,
        string lineEnd = "\r\n")
    {
        string evaluatorClassName = GetEvaluatorClassName(rootType);
        string ns = options.GetNamespace(rootType);

        var ctx = new GenerationContext(lineEnd);

        // Pass 1: collect all subschema info (method names, paths, type declarations).
        var subschemas = new Dictionary<string, SubschemaInfo>();
        CollectSubschemas(rootType, subschemas, "#", "EvaluateRoot");

        EmitFileHeader(ctx, options);
        EmitNamespaceOpen(ctx, ns);
        EmitClassOpen(ctx, evaluatorClassName);

        EmitPathProviderFields(ctx, subschemas);
        EmitPatternRegexFields(ctx, subschemas);

        // Collect and emit property matcher infrastructure (delegate types, UTF-8 constants,
        // per-property methods, PropertySchemaMatchers hash maps, TryGetNamedMatcher wrappers).
        Dictionary<string, PropertyMatcherInfo> propertyMatchers = CollectPropertyMatcherInfo(subschemas);
        EmitPropertyMatcherInfrastructure(ctx, propertyMatchers);

        // Emit public Evaluate<TElement> entry point.
        EmitPublicEvaluateMethod(ctx, rootType);

        // Pass 2: emit evaluation methods for root and all subschemas.
        foreach (KeyValuePair<string, SubschemaInfo> kvp in subschemas)
        {
            ctx.AppendLine();
            EmitSchemaEvaluationMethod(ctx, kvp.Value.TypeDeclaration, kvp.Value.MethodName, subschemas, propertyMatchers);
        }

        EmitDeferredConstantFields(ctx);
        EmitClassClose(ctx);

        string content = ctx.ToString();
        string fileName = evaluatorClassName + ".Evaluator";

        return new GeneratedCodeFile(fileName + options.FileExtension, content);
    }

    private static void CollectSubschemas(
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        string schemaPath,
        string methodName)
    {
        string location = typeDeclaration.LocatedSchema.Location.ToString();

        if (subschemas.ContainsKey(location))
        {
            return;
        }

        // Ensure method name is unique across all subschemas.
        methodName = MakeUniqueMethodName(methodName, subschemas);

        subschemas[location] = new SubschemaInfo(
            methodName,
            schemaPath,
            typeDeclaration,
            typeDeclaration.RequiresItemsEvaluationTracking(),
            typeDeclaration.RequiresPropertyEvaluationTracking());

        if (typeDeclaration.LocatedSchema.IsBooleanSchema)
        {
            return;
        }

        // IHidesSiblingsKeyword check: if $ref hides siblings (draft4-7), only collect the $ref target.
        if (typeDeclaration.HasSiblingHidingKeyword())
        {
            CollectCompositionSubschemas(typeDeclaration, subschemas, schemaPath);
            return;
        }

        CollectCompositionSubschemas(typeDeclaration, subschemas, schemaPath);
        CollectConditionalSubschemas(typeDeclaration, subschemas, schemaPath);
        CollectNotSubschema(typeDeclaration, subschemas, schemaPath);
        CollectPropertySubschemas(typeDeclaration, subschemas, schemaPath);
        CollectArraySubschemas(typeDeclaration, subschemas, schemaPath);
        CollectFallbackSubschemas(typeDeclaration, subschemas, schemaPath);
    }

    private static void CollectCompositionSubschemas(
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        string schemaPath)
    {
        if (typeDeclaration.AllOfCompositionTypes() is { } allOf)
        {
            foreach (var kvp in allOf)
            {
                int index = 0;
                foreach (TypeDeclaration subschemaType in kvp.Value)
                {
                    string path = $"{schemaPath}/{kvp.Key.Keyword}/{index}";
                    string method = MakeMethodName($"AllOf{index}", schemaPath);
                    CollectSubschemas(subschemaType, subschemas, path, method);
                    index++;
                }
            }
        }

        if (typeDeclaration.AnyOfCompositionTypes() is { } anyOf)
        {
            foreach (var kvp in anyOf)
            {
                int index = 0;
                foreach (TypeDeclaration subschemaType in kvp.Value)
                {
                    string path = $"{schemaPath}/{kvp.Key.Keyword}/{index}";
                    string method = MakeMethodName($"AnyOf{index}", schemaPath);
                    CollectSubschemas(subschemaType, subschemas, path, method);
                    index++;
                }
            }
        }

        if (typeDeclaration.OneOfCompositionTypes() is { } oneOf)
        {
            foreach (var kvp in oneOf)
            {
                int index = 0;
                foreach (TypeDeclaration subschemaType in kvp.Value)
                {
                    string path = $"{schemaPath}/{kvp.Key.Keyword}/{index}";
                    string method = MakeMethodName($"OneOf{index}", schemaPath);
                    CollectSubschemas(subschemaType, subschemas, path, method);
                    index++;
                }
            }
        }
    }

    private static void CollectConditionalSubschemas(
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        string schemaPath)
    {
        if (typeDeclaration.IfSubschemaType() is SingleSubschemaKeywordTypeDeclaration ifType)
        {
            string path = $"{schemaPath}/{ifType.Keyword.Keyword}";
            CollectSubschemas(ifType.UnreducedType, subschemas, path, MakeMethodName("If", schemaPath));
        }

        if (typeDeclaration.ThenSubschemaType() is SingleSubschemaKeywordTypeDeclaration thenType)
        {
            string path = $"{schemaPath}/{thenType.Keyword.Keyword}";
            CollectSubschemas(thenType.UnreducedType, subschemas, path, MakeMethodName("Then", schemaPath));
        }

        if (typeDeclaration.ElseSubschemaType() is SingleSubschemaKeywordTypeDeclaration elseType)
        {
            string path = $"{schemaPath}/{elseType.Keyword.Keyword}";
            CollectSubschemas(elseType.UnreducedType, subschemas, path, MakeMethodName("Else", schemaPath));
        }
    }

    private static void CollectNotSubschema(
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        string schemaPath)
    {
        foreach (INotValidationKeyword keyword in typeDeclaration.Keywords().OfType<INotValidationKeyword>())
        {
            if (keyword.TryGetNotType(typeDeclaration, out ReducedTypeDeclaration? notType) &&
                notType is ReducedTypeDeclaration notDecl)
            {
                string path = $"{schemaPath}/{keyword.Keyword}";
                CollectSubschemas(notDecl.ReducedType, subschemas, path, MakeMethodName("Not", schemaPath));
            }
        }
    }

    /// <summary>
    /// Strips the "#/" or "#" prefix from a JSON Reference fragment returned by GetPathModifier.
    /// GetPathModifier returns full fragment references (e.g. "#/properties/foo") but our schemaPath
    /// already tracks the root "#", so we need bare segments for concatenation.
    /// </summary>
    private static string StripFragmentPrefix(string pathModifier)
    {
        if (pathModifier.StartsWith("#/", StringComparison.Ordinal))
        {
            return pathModifier[2..];
        }

        if (pathModifier.StartsWith("#", StringComparison.Ordinal))
        {
            return pathModifier[1..];
        }

        return pathModifier;
    }

    /// <summary>
    /// Percent-encodes each segment of a path (split by '/') so that the resulting
    /// string is valid for use in a URI fragment. Characters like '^', '[', ']' etc.
    /// in patternProperties regex patterns are encoded (e.g., "^a" → "%5Ea").
    /// Characters allowed in URI fragment pchar (unreserved, sub-delims, ':', '@')
    /// are preserved. JSON Pointer escape sequences (~0, ~1) also pass through.
    /// </summary>
    private static string PercentEncodePathSegments(string path)
    {
        string[] segments = path.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = PercentEncodeFragmentSegment(segments[i]);
        }

        return string.Join("/", segments);
    }

    private static string PercentEncodeFragmentSegment(string segment)
    {
        var sb = new StringBuilder(segment.Length);
        foreach (char c in segment)
        {
            if (IsFragmentPchar(c))
            {
                sb.Append(c);
            }
            else
            {
                // Percent-encode as UTF-8 bytes.
                foreach (byte b in System.Text.Encoding.UTF8.GetBytes(new[] { c }))
                {
                    sb.Append('%');
                    sb.Append(b.ToString("X2"));
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if the character is allowed unencoded in a URI fragment pchar
    /// (RFC 3986: unreserved / sub-delims / ":" / "@").
    /// </summary>
    private static bool IsFragmentPchar(char c)
    {
        // unreserved: ALPHA / DIGIT / "-" / "." / "_" / "~"
        if (char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_' || c == '~')
        {
            return true;
        }

        // sub-delims: "!" / "$" / "&" / "'" / "(" / ")" / "*" / "+" / "," / ";" / "="
        // plus ":" / "@"
        return c is '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=' or ':' or '@';
    }

    private static void CollectPropertySubschemas(
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        string schemaPath)
    {
        foreach (PropertyDeclaration prop in typeDeclaration.PropertyDeclarations)
        {
            if (prop.LocalOrComposed == LocalOrComposed.Local && prop.Keyword is IObjectPropertyValidationKeyword propKw)
            {
                string pathMod = StripFragmentPrefix(propKw.GetPathModifier(prop));
                string path = $"{schemaPath}/{pathMod}";
                CollectSubschemas(prop.UnreducedPropertyType, subschemas, path, MakeMethodName(SanitizeForIdentifier(pathMod), schemaPath));
            }
        }

        // propertyNames subschema
        foreach (IObjectPropertyNameSubschemaValidationKeyword keyword in typeDeclaration.Keywords().OfType<IObjectPropertyNameSubschemaValidationKeyword>())
        {
            if (keyword.TryGetPropertyNameDeclaration(typeDeclaration, out TypeDeclaration? propertyNamesType) &&
                propertyNamesType is not null)
            {
                string path = $"{schemaPath}/{keyword.Keyword}";
                CollectSubschemas(propertyNamesType, subschemas, path, MakeMethodName("PropertyNames", schemaPath));
            }
        }

        // patternProperties subschemas
        if (typeDeclaration.PatternProperties() is { } patternProperties)
        {
            foreach (IReadOnlyCollection<PatternPropertyDeclaration> patternPropertyCollection in patternProperties.Values)
            {
                foreach (PatternPropertyDeclaration patternProp in patternPropertyCollection)
                {
                    string pathMod = StripFragmentPrefix(patternProp.Keyword.GetPathModifier(patternProp.Pattern, patternProp.UnreducedPatternPropertyType.ReducedTypeDeclaration()));
                    string path = $"{schemaPath}/{pathMod}";
                    CollectSubschemas(patternProp.UnreducedPatternPropertyType, subschemas, path, MakeMethodName(SanitizeForIdentifier(pathMod), schemaPath));
                }
            }
        }

        // dependentSchemas subschemas
        if (typeDeclaration.DependentSchemasSubschemaTypes() is { } dependentSchemas)
        {
            foreach (IReadOnlyCollection<DependentSchemaDeclaration> depSchemaCollection in dependentSchemas.Values)
            {
                foreach (DependentSchemaDeclaration depSchema in depSchemaCollection)
                {
                    string pathMod = StripFragmentPrefix(depSchema.KeywordPathModifier);
                    string path = $"{schemaPath}/{pathMod}";
                    CollectSubschemas(depSchema.UnreducedDepdendentSchemaType, subschemas, path, MakeMethodName(SanitizeForIdentifier(pathMod), schemaPath));
                }
            }
        }
    }

    private static void CollectArraySubschemas(
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        string schemaPath)
    {
        // Non-tuple items
        foreach (IArrayItemsTypeProviderKeyword keyword in typeDeclaration.Keywords().OfType<IArrayItemsTypeProviderKeyword>())
        {
            if (keyword.TryGetArrayItemsType(typeDeclaration, out ArrayItemsTypeDeclaration? itemsType) &&
                itemsType is ArrayItemsTypeDeclaration items)
            {
                string path = $"{schemaPath}/{StripFragmentPrefix(keyword.GetPathModifier(items))}";
                CollectSubschemas(items.UnreducedType, subschemas, path, MakeMethodName("Items", schemaPath));
            }
        }

        // Tuple items (prefixItems)
        foreach (ITupleTypeProviderKeyword keyword in typeDeclaration.Keywords().OfType<ITupleTypeProviderKeyword>())
        {
            if (keyword.TryGetTupleType(typeDeclaration, out TupleTypeDeclaration? tupleType) &&
                tupleType is TupleTypeDeclaration tuple)
            {
                for (int index = 0; index < tuple.ItemsTypes.Length; index++)
                {
                    ReducedTypeDeclaration item = tuple.ItemsTypes[index];
                    TypeDeclaration unreducedItem = tuple.UnreducedTypes[index];
                    string path = $"{schemaPath}/{StripFragmentPrefix(keyword.GetPathModifier(item, index))}";
                    CollectSubschemas(unreducedItem, subschemas, path, MakeMethodName($"PrefixItems{index}", schemaPath));
                }
            }
        }

        // Contains
        foreach (IArrayContainsValidationKeyword keyword in typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>())
        {
            if (keyword.TryGetContainsItemType(typeDeclaration, out ArrayItemsTypeDeclaration? containsType) &&
                containsType is ArrayItemsTypeDeclaration contains)
            {
                string path = $"{schemaPath}/{keyword.Keyword}";
                CollectSubschemas(contains.UnreducedType, subschemas, path, MakeMethodName("Contains", schemaPath));
            }
        }
    }

    private static void CollectFallbackSubschemas(
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        string schemaPath)
    {
        // additionalProperties (ILocalEvaluatedPropertyValidationKeyword)
        foreach (ILocalEvaluatedPropertyValidationKeyword keyword in typeDeclaration.Keywords().OfType<ILocalEvaluatedPropertyValidationKeyword>())
        {
            if (keyword.TryGetFallbackObjectPropertyType(typeDeclaration, out FallbackObjectPropertyType? fallbackType) &&
                fallbackType is FallbackObjectPropertyType fallback)
            {
                string path = $"{schemaPath}/{((IKeyword)keyword).Keyword}";
                CollectSubschemas(fallback.UnreducedType, subschemas, path, MakeMethodName("AdditionalProperties", schemaPath));
            }
        }

        // unevaluatedProperties (ILocalAndAppliedEvaluatedPropertyValidationKeyword)
        foreach (ILocalAndAppliedEvaluatedPropertyValidationKeyword keyword in typeDeclaration.Keywords().OfType<ILocalAndAppliedEvaluatedPropertyValidationKeyword>())
        {
            if (keyword.TryGetFallbackObjectPropertyType(typeDeclaration, out FallbackObjectPropertyType? fallbackType) &&
                fallbackType is FallbackObjectPropertyType fallback)
            {
                string path = $"{schemaPath}/{((IKeyword)keyword).Keyword}";
                CollectSubschemas(fallback.UnreducedType, subschemas, path, MakeMethodName("UnevaluatedProperties", schemaPath));
            }
        }

        // unevaluatedItems
        foreach (IArrayItemsTypeProviderKeyword keyword in typeDeclaration.Keywords().OfType<IArrayItemsTypeProviderKeyword>())
        {
            // Only collect if it is specifically for unevaluated items.
            if (keyword is IKeyword kw && kw.Keyword == "unevaluatedItems" &&
                keyword.TryGetArrayItemsType(typeDeclaration, out ArrayItemsTypeDeclaration? unevalItems) &&
                unevalItems is ArrayItemsTypeDeclaration unevalItemsType)
            {
                string path = $"{schemaPath}/unevaluatedItems";
                CollectSubschemas(unevalItemsType.UnreducedType, subschemas, path, MakeMethodName("UnevaluatedItems", schemaPath));
            }
        }
    }

    private static string MakeMethodName(string suffix, string basePath)
    {
        if (basePath == "#")
        {
            return $"Evaluate{suffix}";
        }

        // For nested subschemas, include the parent path to avoid name collisions.
        // Strip "#/" prefix and convert path separators to PascalCase.
        string pathPart = basePath;
        if (pathPart.StartsWith("#/", StringComparison.Ordinal))
        {
            pathPart = pathPart[2..];
        }
        else if (pathPart.StartsWith("#", StringComparison.Ordinal))
        {
            pathPart = pathPart[1..];
        }

        // Convert path like "properties/foo/not" to "PropfooNot"
        string sanitized = SanitizeForIdentifier(pathPart);
        return $"Evaluate{sanitized}{suffix}";
    }

    private static string MakeUniqueMethodName(string methodName, Dictionary<string, SubschemaInfo> subschemas)
    {
        // Check if any existing subschema already uses this method name.
        bool collision = false;
        foreach (KeyValuePair<string, SubschemaInfo> kvp in subschemas)
        {
            if (kvp.Value.MethodName == methodName)
            {
                collision = true;
                break;
            }
        }

        if (!collision)
        {
            return methodName;
        }

        // Append a numeric suffix to make it unique.
        int suffix = 1;
        while (true)
        {
            string candidate = $"{methodName}_{suffix}";
            bool found = false;
            foreach (KeyValuePair<string, SubschemaInfo> kvp in subschemas)
            {
                if (kvp.Value.MethodName == candidate)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string SanitizeForIdentifier(string path)
    {
        // Convert a JSON Pointer path like "properties/foo" into a PascalCase identifier part like "PropertiesFoo".
        var sb = new StringBuilder(path.Length);
        bool capitalizeNext = true;

        foreach (char c in path)
        {
            if (c == '/' || c == '-' || c == '_' || c == '~')
            {
                capitalizeNext = true;
            }
            else if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        return sb.ToString();
    }

    private static void EmitPathProviderFields(GenerationContext ctx, Dictionary<string, SubschemaInfo> subschemas)
    {
        foreach (KeyValuePair<string, SubschemaInfo> kvp in subschemas)
        {
            if (kvp.Value.MethodName == "EvaluateRoot")
            {
                continue;
            }

            // Evaluation path field — uses TryCopyPath (strips #/) for evaluation path segments.
            // Percent-encode segments so regex metacharacters (e.g. "^" in patternProperties) are valid URI chars.
            string evalFieldName = kvp.Value.MethodName.Replace("Evaluate", string.Empty) + "EvaluationPath";
            kvp.Value.PathFieldName = evalFieldName;
            string encodedEvalPath = PercentEncodePathSegments(kvp.Value.SchemaPath);
            ctx.AppendLine($"private static readonly JsonSchemaPathProvider {evalFieldName} = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath({FormatUtf8Literal(encodedEvalPath)}, buffer, out written);");

            // Schema evaluation path field — derived from the TypeDeclaration's actual location
            // in the schema document (not the keyword-derived evaluation path). This handles $ref
            // correctly: $ref targets get their actual location (e.g., /$defs/foo) rather than
            // the evaluation path (e.g., /$ref/0).
            string schemaPath = PercentEncodePathSegments(GetSchemaLocationFragment(kvp.Value.TypeDeclaration));

            string schemaFieldName = kvp.Value.MethodName.Replace("Evaluate", string.Empty) + "SchemaPath";
            kvp.Value.SchemaPathFieldName = schemaFieldName;
            ctx.AppendLine($"private static readonly JsonSchemaPathProvider {schemaFieldName} = static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage({FormatUtf8Literal(schemaPath)}, buffer, out written);");
        }

        ctx.AppendLine();
    }

    private static void EmitPatternRegexFields(GenerationContext ctx, Dictionary<string, SubschemaInfo> subschemas)
    {
        var emittedPatterns = new HashSet<string>();
        foreach (SubschemaInfo info in subschemas.Values)
        {
            EmitPatternRegexFieldsForType(ctx, info.TypeDeclaration, emittedPatterns);
        }
    }

    private static void EmitPatternRegexFieldsForType(GenerationContext ctx, TypeDeclaration typeDeclaration, HashSet<string> emittedPatterns)
    {
        if (typeDeclaration.PatternProperties() is { } patternPropsDict)
        {
            foreach (IReadOnlyCollection<PatternPropertyDeclaration> patternPropertyCollection in patternPropsDict.Values)
            {
                foreach (PatternPropertyDeclaration patternProp in patternPropertyCollection)
                {
                    // Only emit a Regex field for patterns that require full regex evaluation.
                    if (CodeGenerationExtensions.ClassifyRegexPattern(patternProp.Pattern) != RegexPatternCategory.FullRegex)
                    {
                        continue;
                    }

                    string fieldName = $"PatternRegex_{MakeSafeIdentifier(patternProp.Pattern)}";
                    if (emittedPatterns.Add(fieldName))
                    {
                        string quotedPattern = SymbolDisplay.FormatLiteral(patternProp.Pattern, true);
                        ctx.AppendLine($"private static readonly System.Text.RegularExpressions.Regex {fieldName} = new({quotedPattern}, System.Text.RegularExpressions.RegexOptions.Compiled);");
                    }
                }
            }
        }

        // String regex validation keywords (e.g., "pattern" keyword).
        foreach (IStringRegexValidationProviderKeyword keyword in typeDeclaration.Keywords().OfType<IStringRegexValidationProviderKeyword>())
        {
            if (keyword.TryGetValidationRegularExpressions(typeDeclaration, out IReadOnlyList<string>? expressions) && expressions.Count > 0)
            {
                // Only emit a Regex field for patterns that require full regex evaluation.
                if (CodeGenerationExtensions.ClassifyRegexPattern(expressions[0]) != RegexPatternCategory.FullRegex)
                {
                    continue;
                }

                string fieldName = "PatternRegex_" + MakeSafeIdentifier(keyword.Keyword);
                if (emittedPatterns.Add(fieldName))
                {
                    string quotedPattern = SymbolDisplay.FormatLiteral(expressions[0], true);
                    ctx.AppendLine($"private static readonly System.Text.RegularExpressions.Regex {fieldName} = new({quotedPattern}, System.Text.RegularExpressions.RegexOptions.Compiled);");
                }
            }
        }
    }

    /// <summary>
    /// Extracts the JSON Pointer fragment from a TypeDeclaration's location for use as the
    /// schema evaluation path. Returns the fragment without "#" but with leading "/"
    /// (e.g., "/$defs/foo"). The annotation producer prepends "#" to form the full location.
    /// </summary>
    private static string GetSchemaLocationFragment(TypeDeclaration typeDeclaration)
    {
        JsonReference location = typeDeclaration.LocatedSchema.Location;
        if (location.HasFragment)
        {
            string fragment = location.Fragment.ToString();

            // Fragment includes the "#" prefix — strip it.
            if (fragment.StartsWith("#", StringComparison.Ordinal))
            {
                fragment = fragment[1..];
            }

            return fragment;
        }

        return string.Empty;
    }

    private static Dictionary<string, PropertyMatcherInfo> CollectPropertyMatcherInfo(
        Dictionary<string, SubschemaInfo> subschemas)
    {
        Dictionary<string, PropertyMatcherInfo> result = [];

        foreach (KeyValuePair<string, SubschemaInfo> kvp in subschemas)
        {
            TypeDeclaration typeDeclaration = kvp.Value.TypeDeclaration;

            // Collect local named properties with subschema validation.
            List<(PropertyDeclaration Property, SubschemaInfo Info)> properties = [];
            foreach (PropertyDeclaration prop in typeDeclaration.PropertyDeclarations)
            {
                if (prop.LocalOrComposed == LocalOrComposed.Local && prop.Keyword is IObjectPropertyValidationKeyword)
                {
                    string subLoc = prop.UnreducedPropertyType.LocatedSchema.Location.ToString();
                    if (subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
                    {
                        properties.Add((prop, info));
                    }
                }
            }

            if (properties.Count == 0)
            {
                continue;
            }

            // Collect required properties from this list.
            List<(PropertyDeclaration Property, SubschemaInfo Info)> requiredProperties = [];
            foreach ((PropertyDeclaration property, SubschemaInfo info) in properties)
            {
                if (property.RequiredOrOptional == RequiredOrOptional.Required)
                {
                    requiredProperties.Add((property, info));
                }
            }

            var matcherInfo = new PropertyMatcherInfo(kvp.Value.MethodName);

            foreach ((PropertyDeclaration property, SubschemaInfo info) in properties)
            {
                int requiredIndex = requiredProperties.FindIndex(r => r.Property == property);
                matcherInfo.Properties.Add(new PropertyMatcherEntry(
                    property.JsonPropertyName,
                    info,
                    requiredIndex));
            }

            DeduplicatePropertyIdentifiers(matcherInfo.Properties);

            result[kvp.Key] = matcherInfo;
        }

        return result;
    }

    private static void EmitPropertyMatcherInfrastructure(
        GenerationContext ctx,
        Dictionary<string, PropertyMatcherInfo> propertyMatchers)
    {
        foreach (KeyValuePair<string, PropertyMatcherInfo> kvp in propertyMatchers)
        {
            PropertyMatcherInfo matcherInfo = kvp.Value;

            // Emit UTF-8 property name constants.
            ctx.AppendLine();
            foreach (PropertyMatcherEntry entry in matcherInfo.Properties)
            {
                string quotedName = SymbolDisplay.FormatLiteral(entry.JsonPropertyName, true);
                ctx.AppendLine($"private static ReadOnlySpan<byte> {entry.Utf8FieldName(matcherInfo.MethodName)} => {quotedName}u8;");
            }

            // Emit delegate type.
            ctx.AppendLine();
            ctx.AppendLine($"private delegate void {matcherInfo.DelegateTypeName}(");
            ctx.PushIndent();
            ctx.AppendLine("IJsonDocument parentDocument,");
            ctx.AppendLine("int currentIndex,");
            ctx.AppendLine("int propertyCount,");
            ctx.AppendLine("ref JsonSchemaContext context,");
            ctx.AppendLine("Span<uint> requiredBits);");
            ctx.PopIndent();

            // Emit per-property validator methods.
            foreach (PropertyMatcherEntry entry in matcherInfo.Properties)
            {
                EmitPerPropertyMethod(ctx, matcherInfo, entry);
            }

            // Emit matchers / TryGetNamedMatcher.
            if (matcherInfo.UseMap)
            {
                EmitMatchersMap(ctx, matcherInfo);
            }

            EmitTryGetNamedMatcher(ctx, matcherInfo);
        }
    }

    private static void EmitPerPropertyMethod(
        GenerationContext ctx,
        PropertyMatcherInfo matcherInfo,
        PropertyMatcherEntry entry)
    {
        string methodName = entry.MatchMethodName(matcherInfo.MethodName);
        string utf8Field = entry.Utf8FieldName(matcherInfo.MethodName);
        string pathField = entry.Info.PathFieldName ?? "null";
        string schemaPathField = entry.Info.SchemaPathFieldName ?? "null";

        ctx.AppendLine();
        ctx.AppendLine($"private static void {methodName}(");
        ctx.PushIndent();
        ctx.AppendLine("IJsonDocument parentDocument,");
        ctx.AppendLine("int currentIndex,");
        ctx.AppendLine("int propertyCount,");
        ctx.AppendLine("ref JsonSchemaContext context,");
        ctx.AppendLine("Span<uint> requiredBits)");
        ctx.PopIndent();
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("context.AddLocalEvaluatedProperty(propertyCount);");
        ctx.AppendLine("JsonSchemaContext propCtx =");
        ctx.PushIndent();
        ctx.AppendLine($"context.PushChildContextUnescaped(parentDocument, currentIndex, useEvaluatedItems: {BoolLiteral(entry.Info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(entry.Info.UseEvaluatedProperties)}, {utf8Field}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
        ctx.PopIndent();
        ctx.AppendLine($"{entry.Info.MethodName}(parentDocument, currentIndex, ref propCtx);");
        ctx.AppendLine("context.CommitChildContext(propCtx.IsMatch, ref propCtx);");

        ctx.AppendLine();
        ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("return;");
        ctx.PopIndent();
        ctx.AppendLine("}");

        // Set required bit if this property is required.
        if (entry.RequiredIndex >= 0)
        {
            int offset = entry.RequiredIndex / 32;
            int bit = entry.RequiredIndex % 32;
            ctx.AppendLine();
            ctx.AppendLine($"requiredBits[{offset}] |= 0x{(1u << bit):X8};");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitMatchersMap(
        GenerationContext ctx,
        PropertyMatcherInfo matcherInfo)
    {
        string delegateName = matcherInfo.DelegateTypeName;
        string builderName = $"MatchersBuilder_{matcherInfo.MethodName}";
        string matchersName = $"Matchers_{matcherInfo.MethodName}";

        ctx.AppendLine();
        ctx.AppendLine($"private static PropertySchemaMatchers<{delegateName}> {builderName}()");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"return new PropertySchemaMatchers<{delegateName}>([");
        ctx.PushIndent();

        foreach (PropertyMatcherEntry entry in matcherInfo.Properties)
        {
            ctx.AppendLine($"(static () => {entry.Utf8FieldName(matcherInfo.MethodName)}, {entry.MatchMethodName(matcherInfo.MethodName)}),");
        }

        ctx.PopIndent();
        ctx.AppendLine("]);");
        ctx.PopIndent();
        ctx.AppendLine("}");

        ctx.AppendLine();
        ctx.AppendLine($"private static PropertySchemaMatchers<{delegateName}> {matchersName} {{ get; }} = {builderName}();");
    }

    private static void EmitTryGetNamedMatcher(
        GenerationContext ctx,
        PropertyMatcherInfo matcherInfo)
    {
        string delegateName = matcherInfo.DelegateTypeName;
        string tryGetName = matcherInfo.TryGetMethodName;

        ctx.AppendLine();
        ctx.AppendLine($"private static bool {tryGetName}(");
        ctx.PushIndent();
        ctx.AppendLine("ReadOnlySpan<byte> span,");
        ctx.AppendLine("#if NET");
        ctx.AppendLine("[NotNullWhen(true)]");
        ctx.AppendLine("#endif");
        ctx.AppendLine($"out {delegateName}? matcher)");
        ctx.PopIndent();
        ctx.AppendLine("{");
        ctx.PushIndent();

        if (matcherInfo.UseMap)
        {
            // Use the PropertySchemaMatchers hash map.
            string matchersName = $"Matchers_{matcherInfo.MethodName}";
            ctx.AppendLine($"return {matchersName}.TryGetNamedMatcher(span, out matcher);");
        }
        else
        {
            // Single property: use simple SequenceEqual.
            foreach (PropertyMatcherEntry entry in matcherInfo.Properties)
            {
                ctx.AppendLine($"if (span.SequenceEqual({entry.Utf8FieldName(matcherInfo.MethodName)}))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"matcher = {entry.MatchMethodName(matcherInfo.MethodName)};");
                ctx.AppendLine("return true;");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine();
            }

            ctx.AppendLine("matcher = default;");
            ctx.AppendLine("return false;");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitPublicEvaluateMethod(GenerationContext ctx, TypeDeclaration rootType)
    {
        bool useEvaluatedItems = rootType.RequiresItemsEvaluationTracking();
        bool useEvaluatedProperties = rootType.RequiresPropertyEvaluationTracking();

        ctx.AppendLine("/// <summary>");
        ctx.AppendLine("/// Evaluates the given JSON element against this schema.");
        ctx.AppendLine("/// </summary>");
        ctx.AppendLine("/// <typeparam name=\"TElement\">The type of JSON element.</typeparam>");
        ctx.AppendLine("/// <param name=\"instance\">The instance to evaluate.</param>");
        ctx.AppendLine("/// <param name=\"resultsCollector\">The optional results collector.</param>");
        ctx.AppendLine("/// <returns><see langword=\"true\"/> if the instance is valid against the schema.</returns>");
        ctx.AppendLine("public static bool Evaluate<TElement>(");
        ctx.PushIndent();
        ctx.AppendLine("in TElement instance,");
        ctx.AppendLine("IJsonSchemaResultsCollector? resultsCollector = null)");
        ctx.PopIndent();
        ctx.PushIndent();
        ctx.AppendLine("where TElement : struct, IJsonElement<TElement>");
        ctx.PopIndent();
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("JsonSchemaContext context = JsonSchemaContext.BeginContext(");
        ctx.PushIndent();
        ctx.AppendLine("instance.ParentDocument,");
        ctx.AppendLine("instance.ParentDocumentIndex,");
        ctx.AppendLine($"usingEvaluatedItems: {BoolLiteral(useEvaluatedItems)},");
        ctx.AppendLine($"usingEvaluatedProperties: {BoolLiteral(useEvaluatedProperties)},");
        ctx.AppendLine("resultsCollector: resultsCollector);");
        ctx.PopIndent();
        ctx.AppendLine();

        ctx.AppendLine("try");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("EvaluateRoot(instance.ParentDocument, instance.ParentDocumentIndex, ref context);");
        ctx.AppendLine("context.EndContext();");
        ctx.AppendLine("return context.IsMatch;");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("finally");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("context.Dispose();");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine();
    }

    private static void EmitSchemaEvaluationMethod(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        string methodName,
        Dictionary<string, SubschemaInfo> subschemas,
        Dictionary<string, PropertyMatcherInfo> propertyMatchers)
    {
        if (typeDeclaration.LocatedSchema.IsBooleanSchema)
        {
            EmitBooleanSchemaMethod(ctx, typeDeclaration, methodName);
            return;
        }

        ctx.AppendLine($"private static void {methodName}(");
        ctx.PushIndent();
        ctx.AppendLine("IJsonDocument parentDocument,");
        ctx.AppendLine("int parentIndex,");
        ctx.AppendLine("ref JsonSchemaContext context)");
        ctx.PopIndent();
        ctx.AppendLine("{");
        ctx.PushIndent();

        bool needsTokenType = RequiresTokenType(typeDeclaration);
        if (needsTokenType)
        {
            ctx.AppendLine("JsonTokenType tokenType = parentDocument.GetJsonTokenType(parentIndex);");
            ctx.AppendLine();
        }

        ctx.AppendLine("Debug.Assert(parentDocument.GetJsonTokenType(parentIndex) is not");
        ctx.PushIndent();
        ctx.AppendLine("(JsonTokenType.None or");
        ctx.AppendLine("JsonTokenType.EndObject or");
        ctx.AppendLine("JsonTokenType.EndArray));");
        ctx.PopIndent();

        // Check for IHidesSiblingsKeyword ($ref in draft4-7 that hides all siblings).
        if (typeDeclaration.HasSiblingHidingKeyword())
        {
            EmitHidesSiblingsRef(ctx, typeDeclaration, subschemas);
            ctx.PopIndent();
            ctx.AppendLine("}");
            return;
        }

        // Emit keyword handlers in priority order.
        int shortCircuitPos = ctx.Length;

        // Priority: CoreType (1000) — type validation
        EmitTypeValidation(ctx, typeDeclaration);
        EmitShortCircuit(ctx, ref shortCircuitPos);

        // Priority: Default (~2^31) — const, enum, string, number, format
        EmitConstValidation(ctx, typeDeclaration);
        EmitEnumValidation(ctx, typeDeclaration);
        EmitStringValidation(ctx, typeDeclaration);
        EmitNumberValidation(ctx, typeDeclaration);
        EmitFormatValidation(ctx, typeDeclaration);
        EmitShortCircuit(ctx, ref shortCircuitPos);

        // Priority: Composition (~2^31+1000) — allOf, anyOf, oneOf, not, if/then/else
        EmitCompositionValidation(ctx, typeDeclaration, subschemas);
        EmitIfThenElseValidation(ctx, typeDeclaration, subschemas);
        EmitShortCircuit(ctx, ref shortCircuitPos);

        // Priority: AfterComposition (~2^31+2000) — object, array
        string location = typeDeclaration.LocatedSchema.Location.ToString();
        propertyMatchers.TryGetValue(location, out PropertyMatcherInfo? matcherInfo);
        EmitObjectValidation(ctx, typeDeclaration, subschemas, matcherInfo);
        EmitArrayValidation(ctx, typeDeclaration, subschemas);
        EmitShortCircuit(ctx, ref shortCircuitPos);

        // Priority: Last — unevaluated
        EmitUnevaluatedValidation(ctx, typeDeclaration, subschemas);

        // After all validation — annotations
        EmitAnnotations(ctx, typeDeclaration);

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitBooleanSchemaMethod(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        string methodName)
    {
        bool isTrue = typeDeclaration.LocatedSchema.Schema.ValueKind == JsonValueKind.True;

        ctx.AppendLine($"private static void {methodName}(");
        ctx.PushIndent();
        ctx.AppendLine("IJsonDocument parentDocument,");
        ctx.AppendLine("int parentIndex,");
        ctx.AppendLine("ref JsonSchemaContext context)");
        ctx.PopIndent();
        ctx.AppendLine("{");
        ctx.PushIndent();

        if (!isTrue)
        {
            ctx.AppendLine("context.EvaluatedBooleanSchema(false);");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitHidesSiblingsRef(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        // Find the $ref target via allOf composition (IReferenceKeyword implements IAllOfSubschemaValidationKeyword).
        if (typeDeclaration.AllOfCompositionTypes() is { } allOf)
        {
            foreach (var kvp in allOf)
            {
                foreach (TypeDeclaration subschemaType in kvp.Value)
                {
                    string subLoc = subschemaType.LocatedSchema.Location.ToString();
                    if (subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
                    {
                        ctx.AppendLine();
                        ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref context);");
                    }
                }
            }
        }
    }

    private static void EmitShortCircuit(GenerationContext ctx, ref int lastShortCircuitPosition)
    {
        if (ctx.Length == lastShortCircuitPosition)
        {
            // Nothing was emitted since the last short-circuit; skip duplicate.
            return;
        }

        ctx.AppendLine();
        ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("return;");
        ctx.PopIndent();
        ctx.AppendLine("}");

        lastShortCircuitPosition = ctx.Length;
    }

    private static bool RequiresTokenType(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.Keywords().Any(k =>
            k is ICoreTypeValidationKeyword or
                 IObjectValidationKeyword or
                 IArrayValidationKeyword or
                 IStringValidationKeyword or
                 INumberValidationKeyword or
                 IFormatProviderKeyword or
                 ISingleConstantValidationKeyword or
                 IAnyOfConstantValidationKeyword);
    }

    private static void EmitTypeValidation(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        CoreTypes allowedTypes = typeDeclaration.AllowedCoreTypes();
        if (allowedTypes == CoreTypes.None || allowedTypes == CoreTypes.Any)
        {
            return;
        }

        string typeKeywordLiteral = GetTypeKeywordLiteral(typeDeclaration);

        if (TryGetSingleCoreType(allowedTypes, out CoreTypes singleType))
        {
            EmitSingleTypeValidation(ctx, typeDeclaration, singleType, typeKeywordLiteral);
        }
        else
        {
            EmitMultiTypeValidation(ctx, typeDeclaration, allowedTypes, typeKeywordLiteral);
        }
    }

    private static void EmitSingleTypeValidation(GenerationContext ctx, TypeDeclaration typeDeclaration, CoreTypes singleType, string typeKeywordLiteral)
    {
        string matchMethod = GetMatchMethodName(singleType);
        string ignoredField = GetIgnoredNotTypeName(singleType);

        ctx.AppendLine();

        if (singleType == CoreTypes.Integer)
        {
            ctx.AppendLine("int integerTypeExponent = 0;");
            ctx.AppendLine("if (tokenType == JsonTokenType.Number)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("ReadOnlyMemory<byte> typeCheckRawValue = parentDocument.GetRawSimpleValue(parentIndex);");
            ctx.AppendLine("JsonElementHelpers.TryParseNumber(typeCheckRawValue.Span, out _, out _, out _, out integerTypeExponent);");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine();
            ctx.AppendLine($"if (!JsonSchemaEvaluation.{matchMethod}(tokenType, {typeKeywordLiteral}, integerTypeExponent, ref context))");
        }
        else
        {
            ctx.AppendLine($"if (!JsonSchemaEvaluation.{matchMethod}(tokenType, {typeKeywordLiteral}, ref context))");
        }

        ctx.AppendLine("{");
        ctx.PushIndent();

        ctx.AppendLine("if (!context.HasCollector)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("return;");
        ctx.PopIndent();
        ctx.AppendLine("}");

        List<string> sensitiveKeywords = GetTypeSensitiveKeywordNames(typeDeclaration, singleType);
        foreach (string kw in sensitiveKeywords)
        {
            ctx.AppendLine();
            ctx.AppendLine($"context.IgnoredKeyword(JsonSchemaEvaluation.{ignoredField}, {FormatUtf8Literal(kw)});");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitMultiTypeValidation(GenerationContext ctx, TypeDeclaration typeDeclaration, CoreTypes allowedTypes, string typeKeywordLiteral)
    {
        bool hasInteger = (allowedTypes & CoreTypes.Integer) != 0;
        bool hasNumber = (allowedTypes & CoreTypes.Number) != 0;

        ctx.AppendLine();
        ctx.AppendLine("bool typeMatch = false;");

        if ((allowedTypes & CoreTypes.Object) != 0)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.StartObject)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("typeMatch = true;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if ((allowedTypes & CoreTypes.Array) != 0)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.StartArray)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("typeMatch = true;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if ((allowedTypes & CoreTypes.Null) != 0)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.Null)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("typeMatch = true;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if ((allowedTypes & CoreTypes.Boolean) != 0)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType is JsonTokenType.True or JsonTokenType.False)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("typeMatch = true;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if (hasNumber)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.Number)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("typeMatch = true;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }
        else if (hasInteger)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.Number)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("ReadOnlyMemory<byte> typeCheckRawValue = parentDocument.GetRawSimpleValue(parentIndex);");
            ctx.AppendLine("JsonElementHelpers.TryParseNumber(typeCheckRawValue.Span, out _, out _, out _, out int integerTypeExponent);");
            ctx.AppendLine("if (integerTypeExponent == 0)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("typeMatch = true;");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if ((allowedTypes & CoreTypes.String) != 0)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.String)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("typeMatch = true;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        string typeListString = BuildTypeListString(allowedTypes);
        string typeListLiteral = FormatUtf8Literal(typeListString);

        ctx.AppendLine();
        ctx.AppendLine($"context.EvaluatedKeyword(typeMatch, static (buffer, out written) => JsonSchemaEvaluation.ExpectedType({typeListLiteral}, buffer, out written), {typeKeywordLiteral});");

        ctx.AppendLine();
        ctx.AppendLine("if (!typeMatch)");
        ctx.AppendLine("{");
        ctx.PushIndent();

        ctx.AppendLine("if (!context.HasCollector)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("return;");
        ctx.PopIndent();
        ctx.AppendLine("}");

        EmitAllTypeSensitiveIgnoredKeywords(ctx, typeDeclaration);

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static string BuildTypeListString(CoreTypes types)
    {
        var parts = new List<string>();
        if ((types & CoreTypes.Array) != 0)
        {
            parts.Add("\"array\"");
        }

        if ((types & CoreTypes.Object) != 0)
        {
            parts.Add("\"object\"");
        }

        if ((types & CoreTypes.Null) != 0)
        {
            parts.Add("\"null\"");
        }

        if ((types & CoreTypes.Boolean) != 0)
        {
            parts.Add("\"boolean\"");
        }

        if ((types & CoreTypes.Number) != 0)
        {
            parts.Add("\"number\"");
        }

        if ((types & CoreTypes.Integer) != 0 && (types & CoreTypes.Number) == 0)
        {
            parts.Add("\"integer\"");
        }

        if ((types & CoreTypes.String) != 0)
        {
            parts.Add("\"string\"");
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private static void EmitAllTypeSensitiveIgnoredKeywords(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        foreach (IKeyword keyword in typeDeclaration.Keywords())
        {
            string? ignoredField = keyword switch
            {
                IObjectValidationKeyword => "IgnoredNotTypeObject",
                IArrayValidationKeyword => "IgnoredNotTypeArray",
                IStringValidationKeyword => "IgnoredNotTypeString",
                INumberValidationKeyword => "IgnoredNotTypeNumber",
                IBooleanValidationKeyword => "IgnoredNotTypeBoolean",
                INullValidationKeyword => "IgnoredNotTypeNull",
                _ => null,
            };

            if (ignoredField is not null)
            {
                ctx.AppendLine();
                ctx.AppendLine($"context.IgnoredKeyword(JsonSchemaEvaluation.{ignoredField}, {FormatUtf8Literal(keyword.Keyword)});");
            }
        }
    }

    private static void EmitConstValidation(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        ISingleConstantValidationKeyword? constKw = typeDeclaration.Keywords().OfType<ISingleConstantValidationKeyword>().FirstOrDefault();
        if (constKw is null || !constKw.TryGetConstantValue(typeDeclaration, out JsonElement constantValue))
        {
            return;
        }

        string formattedKeyword = FormatUtf8Literal(constKw.Keyword);

        ctx.AppendLine();

        switch (constantValue.ValueKind)
        {
            case JsonValueKind.String:
                EmitStringConstValidation(ctx, constKw, constantValue, formattedKeyword);
                break;
            case JsonValueKind.Number:
                EmitNumberConstValidation(ctx, constantValue, formattedKeyword);
                break;
            case JsonValueKind.True:
                EmitBooleanConstValidation(ctx, true, formattedKeyword);
                break;
            case JsonValueKind.False:
                EmitBooleanConstValidation(ctx, false, formattedKeyword);
                break;
            case JsonValueKind.Null:
                EmitNullConstValidation(ctx, formattedKeyword);
                break;
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                EmitComplexConstValidation(ctx, constantValue, formattedKeyword);
                break;
        }
    }

    private static void EmitStringConstValidation(GenerationContext ctx, ISingleConstantValidationKeyword constKw, JsonElement constantValue, string formattedKeyword)
    {
        string quotedValue = SymbolDisplay.FormatLiteral(constantValue.GetString()!, true);

        ctx.AppendLine("if (tokenType == JsonTokenType.String)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("using UnescapedUtf8JsonString constUnescaped = parentDocument.GetUtf8JsonString(parentIndex, JsonTokenType.String);");
        ctx.AppendLine($"JsonSchemaEvaluation.MatchStringConstantValue(constUnescaped.Span, {quotedValue}u8, {quotedValue}, {formattedKeyword}, ref context);");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(false, {quotedValue}, messageProvider: JsonSchemaEvaluation.ExpectedStringEquals, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitNumberConstValidation(GenerationContext ctx, JsonElement constantValue, string formattedKeyword)
    {
        string rawText = constantValue.GetRawText();
        JsonElementHelpers.ParseNumber(System.Text.Encoding.UTF8.GetBytes(rawText), out bool isNeg, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exp);

        string isNegStr = BoolLiteral(isNeg);
        string integralStr = SymbolDisplay.FormatLiteral(System.Text.Encoding.UTF8.GetString(integral.ToArray()), true);
        string fractionalStr = SymbolDisplay.FormatLiteral(System.Text.Encoding.UTF8.GetString(fractional.ToArray()), true);
        string expStr = exp.ToString();
        string rawValueStr = SymbolDisplay.FormatLiteral(rawText, true);

        ctx.AppendLine("if (tokenType == JsonTokenType.Number)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("ReadOnlyMemory<byte> constRawValue = parentDocument.GetRawSimpleValue(parentIndex);");
        ctx.AppendLine("JsonElementHelpers.TryParseNumber(constRawValue.Span, out bool constIsNeg, out ReadOnlySpan<byte> constIntegral, out ReadOnlySpan<byte> constFractional, out int constExponent);");
        ctx.AppendLine($"JsonSchemaEvaluation.MatchEquals(constIsNeg, constIntegral, constFractional, constExponent, {isNegStr}, {integralStr}u8, {fractionalStr}u8, {expStr}, {rawValueStr}, {formattedKeyword}, ref context);");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(false, {rawValueStr}, messageProvider: JsonSchemaEvaluation.ExpectedEquals, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitBooleanConstValidation(GenerationContext ctx, bool expectation, string formattedKeyword)
    {
        string tokenName = expectation ? "True" : "False";
        ctx.AppendLine($"if (tokenType == JsonTokenType.{tokenName})");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(true, messageProvider: JsonSchemaEvaluation.ExpectedBoolean{tokenName}, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.ExpectedBoolean{tokenName}, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitNullConstValidation(GenerationContext ctx, string formattedKeyword)
    {
        ctx.AppendLine("if (tokenType == JsonTokenType.Null)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(true, messageProvider: JsonSchemaEvaluation.ExpectedNull, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.ExpectedNull, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitComplexConstValidation(GenerationContext ctx, JsonElement constantValue, string formattedKeyword)
    {
        string rawText = constantValue.GetRawText();
        string quotedRawText = SymbolDisplay.FormatLiteral(rawText, true);
        string constField = GetOrCreateConstantField(ctx, rawText);

        ctx.AppendLine($"if (JsonElementHelpers.DeepEqualsNoParentDocumentCheck({constField}, 0, parentDocument, parentIndex))");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(true, {quotedRawText}, messageProvider: JsonSchemaEvaluation.ExpectedConstant, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"context.EvaluatedKeyword(false, {quotedRawText}, messageProvider: JsonSchemaEvaluation.ExpectedConstant, {formattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitEnumValidation(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        IAnyOfConstantValidationKeyword? enumKw = typeDeclaration.Keywords().OfType<IAnyOfConstantValidationKeyword>().FirstOrDefault();
        if (enumKw is null || !enumKw.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants))
        {
            return;
        }

        string formattedKeyword = FormatUtf8Literal(enumKw.Keyword);

        ctx.AppendLine();

        bool hasStringValues = constants.Any(c => c.ValueKind == JsonValueKind.String);
        bool hasNumberValues = constants.Any(c => c.ValueKind == JsonValueKind.Number);
        bool hasBoolValues = constants.Any(c => c.ValueKind is JsonValueKind.True or JsonValueKind.False);
        bool hasNullValues = constants.Any(c => c.ValueKind == JsonValueKind.Null);
        bool hasComplexValues = constants.Any(c => c.ValueKind is JsonValueKind.Object or JsonValueKind.Array);

        if (hasStringValues)
        {
            JsonElement[] stringConstants = constants.Where(c => c.ValueKind == JsonValueKind.String).ToArray();

            ctx.AppendLine("if (tokenType == JsonTokenType.String)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("using UnescapedUtf8JsonString enumUnescaped = parentDocument.GetUtf8JsonString(parentIndex, JsonTokenType.String);");
            ctx.AppendLine();

            if (stringConstants.Length > MinEnumValuesForHashSet)
            {
                string fieldName = $"EnumStringSet_{ctx.DeferredEnumStringSets.Count}";
                string builderName = $"BuildEnumStringSet_{ctx.DeferredEnumStringSets.Count}";
                string[] values = stringConstants.Select(c => c.GetString()!).ToArray();
                ctx.DeferredEnumStringSets.Add((fieldName, builderName, values));

                ctx.AppendLine($"if ({fieldName}.Contains(enumUnescaped.Span))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("goto enumShortCircuitSuccess;");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
            else
            {
                foreach (JsonElement c in stringConstants)
                {
                    string val = SymbolDisplay.FormatLiteral(c.GetString()!, true);
                    ctx.AppendLine($"if (enumUnescaped.Span.SequenceEqual({val}u8))");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("goto enumShortCircuitSuccess;");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.AppendLine();
                }
            }

            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if (hasNumberValues)
        {
            JsonElement[] numberConstants = constants.Where(c => c.ValueKind == JsonValueKind.Number).ToArray();

            // Check if all numeric values are integers within long range for switch optimization.
            long[]? longValues = null;
            bool useSwitch = numberConstants.Length > MinEnumValuesForHashSet && TryGetAllInt64ValuesFromElements(numberConstants, out longValues);

            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.Number)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("ReadOnlyMemory<byte> enumRaw = parentDocument.GetRawSimpleValue(parentIndex);");
            ctx.AppendLine("JsonElementHelpers.TryParseNumber(enumRaw.Span, out bool enumIsNeg, out ReadOnlySpan<byte> enumIntegral, out ReadOnlySpan<byte> enumFractional, out int enumExponent);");
            ctx.AppendLine();

            if (useSwitch)
            {
                ctx.AppendLine("if (JsonElementHelpers.TryGetNormalizedInt64(enumIsNeg, enumIntegral, enumFractional, enumExponent, out long enumLongValue))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("switch (enumLongValue)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                foreach (long v in longValues!)
                {
                    ctx.AppendLine($"case {v}:");
                }

                ctx.PushIndent();
                ctx.AppendLine("goto enumShortCircuitSuccess;");
                ctx.PopIndent();
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
            else
            {
                foreach (JsonElement c in numberConstants)
                {
                    string rawText = c.GetRawText();
                    byte[] rawBytes = System.Text.Encoding.UTF8.GetBytes(rawText);
                    JsonElementHelpers.ParseNumber(rawBytes, out bool isNeg, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exp);
                    string isNegStr = BoolLiteral(isNeg);
                    string integralStr = SymbolDisplay.FormatLiteral(System.Text.Encoding.UTF8.GetString(integral.ToArray()), true);
                    string fractionalStr = SymbolDisplay.FormatLiteral(System.Text.Encoding.UTF8.GetString(fractional.ToArray()), true);
                    string expStr = exp.ToString();

                    ctx.AppendLine($"if (JsonElementHelpers.CompareNormalizedJsonNumbers(enumIsNeg, enumIntegral, enumFractional, enumExponent, {isNegStr}, {integralStr}u8, {fractionalStr}u8, {expStr}) == 0)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("goto enumShortCircuitSuccess;");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.AppendLine();
                }
            }

            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if (hasBoolValues)
        {
            foreach (JsonElement c in constants.Where(c => c.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                string tokenName = c.ValueKind == JsonValueKind.True ? "True" : "False";
                ctx.AppendLine();
                ctx.AppendLine($"if (tokenType == JsonTokenType.{tokenName})");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("goto enumShortCircuitSuccess;");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }

        if (hasNullValues)
        {
            ctx.AppendLine();
            ctx.AppendLine("if (tokenType == JsonTokenType.Null)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("goto enumShortCircuitSuccess;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        if (hasComplexValues)
        {
            foreach (JsonElement c in constants.Where(c => c.ValueKind is JsonValueKind.Object or JsonValueKind.Array))
            {
                string rawText = c.GetRawText();
                string constField = GetOrCreateConstantField(ctx, rawText);
                ctx.AppendLine();
                ctx.AppendLine($"if (JsonElementHelpers.DeepEqualsNoParentDocumentCheck({constField}, 0, parentDocument, parentIndex))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("goto enumShortCircuitSuccess;");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }

        ctx.AppendLine();
        ctx.AppendLine($"context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.DidNotMatchAtLeastOneConstantValue, {formattedKeyword});");
        ctx.AppendLine();
        ctx.AppendLine("if (!context.HasCollector)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("return;");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine();
        ctx.AppendLine("goto enumAfterFailure;");
        ctx.AppendLine();
        ctx.AppendLine("enumShortCircuitSuccess:");
        ctx.AppendLine($"context.EvaluatedKeyword(true, messageProvider: JsonSchemaEvaluation.MatchedAtLeastOneConstantValue, {formattedKeyword});");
        ctx.AppendLine();
        ctx.AppendLine("enumAfterFailure:;");
    }

    private static void EmitStringValidation(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        var lengthKeywords = typeDeclaration.Keywords().OfType<IStringLengthConstantValidationKeyword>().ToList();
        var regexKeywords = typeDeclaration.Keywords().OfType<IStringRegexValidationProviderKeyword>().ToList();

        if (lengthKeywords.Count == 0 && regexKeywords.Count == 0)
        {
            return;
        }

        ctx.AppendLine();
        ctx.AppendLine("if (tokenType == JsonTokenType.String)");
        ctx.AppendLine("{");
        ctx.PushIndent();

        // Get unescaped string once for both length and regex validation.
        ctx.AppendLine("using UnescapedUtf8JsonString unescapedString = parentDocument.GetUtf8JsonString(parentIndex, JsonTokenType.String);");

        if (lengthKeywords.Count > 0)
        {
            ctx.AppendLine("int stringLength = JsonElementHelpers.CountRunes(unescapedString.Span);");

            foreach (IStringLengthConstantValidationKeyword keyword in lengthKeywords)
            {
                if (!keyword.TryGetOperator(typeDeclaration, out Operator op) || op == Operator.None)
                {
                    continue;
                }

                if (!keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants) || constants.Length == 0)
                {
                    continue;
                }

                int expected = (int)constants[0].GetDecimal();
                string opFunc = GetStringLengthOperatorFunction(op);

                ctx.AppendLine($"{opFunc}({expected}, stringLength, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }

        if (regexKeywords.Count > 0)
        {
            foreach (IStringRegexValidationProviderKeyword keyword in regexKeywords)
            {
                if (!keyword.TryGetValidationRegularExpressions(typeDeclaration, out IReadOnlyList<string>? expressions) || expressions.Count == 0)
                {
                    continue;
                }

                string rawPattern = expressions[0];
                string regex = SymbolDisplay.FormatLiteral(rawPattern, true);
                RegexPatternCategory category = CodeGenerationExtensions.ClassifyRegexPattern(rawPattern);

                switch (category)
                {
                    case RegexPatternCategory.Noop:
                        ctx.AppendLine($"JsonSchemaEvaluation.MatchNoopRegularExpression({regex}, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
                        break;

                    case RegexPatternCategory.NonEmpty:
                        ctx.AppendLine($"JsonSchemaEvaluation.MatchNonEmptyRegularExpression(unescapedString.Span, {regex}, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
                        break;

                    case RegexPatternCategory.Prefix:
                    {
                        string prefix = CodeGenerationExtensions.ExtractRegexPrefix(rawPattern);
                        string prefixLiteral = SymbolDisplay.FormatLiteral(prefix, true);
                        ctx.AppendLine($"JsonSchemaEvaluation.MatchPrefixRegularExpression(unescapedString.Span, {prefixLiteral}u8, {regex}, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
                        break;
                    }

                    case RegexPatternCategory.Range:
                    {
                        (int min, int max) = CodeGenerationExtensions.ExtractRegexRange(rawPattern);
                        ctx.AppendLine($"JsonSchemaEvaluation.MatchRangeRegularExpression(unescapedString.Span, {min}, {max}, {regex}, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
                        break;
                    }

                    default:
                    {
                        string fieldName = "PatternRegex_" + MakeSafeIdentifier(keyword.Keyword);
                        ctx.AppendLine($"JsonSchemaEvaluation.MatchRegularExpression(unescapedString.Span, {fieldName}, {regex}, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
                        break;
                    }
                }

                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static string GetStringLengthOperatorFunction(Operator op)
    {
        return op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchLengthEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchLengthNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchLengthLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchLengthLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchLengthGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchLengthGreaterThanOrEquals",
            _ => throw new System.InvalidOperationException($"Unsupported string length operator: {op}"),
        };
    }

    private static void EmitNumberValidation(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        var numberKeywords = typeDeclaration.Keywords().OfType<INumberConstantValidationKeyword>().ToList();
        if (numberKeywords.Count == 0)
        {
            return;
        }

        ctx.AppendLine();
        ctx.AppendLine("if (tokenType == JsonTokenType.Number)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("ReadOnlyMemory<byte> numRawValue = parentDocument.GetRawSimpleValue(parentIndex);");
        ctx.AppendLine("JsonElementHelpers.TryParseNumber(numRawValue.Span, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);");

        foreach (INumberConstantValidationKeyword keyword in numberKeywords)
        {
            if (!keyword.TryGetOperator(typeDeclaration, out Operator op) || op == Operator.None)
            {
                continue;
            }

            if (!keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants) || constants.Length == 0)
            {
                continue;
            }

            ctx.AppendLine();

            string rawText = constants[0].GetRawText();
            byte[] rawBytes = System.Text.Encoding.UTF8.GetBytes(rawText);
            JsonElementHelpers.ParseNumber(rawBytes, out bool isNeg, out ReadOnlySpan<byte> intPart, out ReadOnlySpan<byte> fracPart, out int expVal);

            string isNegStr = BoolLiteral(isNeg);
            string integralStr = SymbolDisplay.FormatLiteral(System.Text.Encoding.UTF8.GetString(intPart.ToArray()), true);
            string fractionalStr = SymbolDisplay.FormatLiteral(System.Text.Encoding.UTF8.GetString(fracPart.ToArray()), true);
            string expStr = expVal.ToString();
            string rawValueStr = SymbolDisplay.FormatLiteral(rawText, true);

            if (op == Operator.MultipleOf)
            {
                string divisor = System.Text.Encoding.UTF8.GetString(intPart.ToArray()) + System.Text.Encoding.UTF8.GetString(fracPart.ToArray());
                ctx.AppendLine($"JsonSchemaEvaluation.MatchMultipleOf(integral, fractional, exponent, {divisor}, {expStr}, {rawValueStr}, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
            }
            else
            {
                string opFunc = GetNumberOperatorFunction(op);
                ctx.AppendLine($"{opFunc}(isNegative, integral, fractional, exponent, {isNegStr}, {integralStr}u8, {fractionalStr}u8, {expStr}, {rawValueStr}, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
            }

            ctx.AppendLine();
            ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static string GetNumberOperatorFunction(Operator op)
    {
        return op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchGreaterThanOrEquals",
            _ => throw new System.InvalidOperationException($"Unsupported number operator: {op}"),
        };
    }

    private static void EmitFormatValidation(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        IFormatProviderKeyword? formatKw = typeDeclaration.Keywords().OfType<IFormatProviderKeyword>().FirstOrDefault();
        if (formatKw is null)
        {
            return;
        }

        string? format = typeDeclaration.ExplicitFormat();
        if (format is null)
        {
            return;
        }

        string formatKeywordLiteral = FormatUtf8Literal(((IKeyword)formatKw).Keyword);

        JsonTokenType? expectedTokenType = FormatHandlerRegistry.Instance.FormatHandlers.GetExpectedTokenType(format);
        if (expectedTokenType is null)
        {
            // Unrecognized format — emit ignored annotation.
            ctx.AppendLine();
            ctx.AppendLine($"context.IgnoredKeyword(JsonSchemaEvaluation.IgnoredUnrecognizedFormat, {formatKeywordLiteral});");
            return;
        }

        // Content keywords (contentEncoding, contentMediaType) are always assertion keywords
        // in pre-2019-09 drafts, even when AlwaysAssertFormat is false (validateFormat: false).
        // In Draft 2019-09+, content keywords are annotation-only and should not assert.
        // IContentSemanticsKeyword (on contentEncoding keywords) tells us the draft directly.
        // IContentMediaTypeValidationKeyword doesn't carry ContentSemantics, so we check the
        // format name suffix or the presence of a pre-2019-09 contentEncoding sibling keyword.
        bool isContentFormat =
            formatKw is IContentSemanticsKeyword { ContentSemantics: ContentEncodingSemantics.PreDraft201909 } ||
            (formatKw is IContentMediaTypeValidationKeyword &&
             (format.EndsWith("-pre201909", System.StringComparison.Ordinal) ||
              typeDeclaration.Keywords().OfType<IContentSemanticsKeyword>().Any(k => k.ContentSemantics == ContentEncodingSemantics.PreDraft201909)));
        if (!isContentFormat && !typeDeclaration.AlwaysAssertFormat())
        {
            // Format is not asserted — the annotation value is emitted by EmitAnnotations.
            return;
        }

        string? matchMethodName = GetFormatMatchMethodName(format);
        if (matchMethodName is null)
        {
            // No match method for this format — emit ignored annotation.
            ctx.AppendLine();
            ctx.AppendLine($"context.IgnoredKeyword(JsonSchemaEvaluation.IgnoredUnrecognizedFormat, {formatKeywordLiteral});");
            return;
        }

        ctx.AppendLine();

        if (expectedTokenType == JsonTokenType.String)
        {
            ctx.AppendLine("if (tokenType == JsonTokenType.String)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("using UnescapedUtf8JsonString unescapedFormatString = parentDocument.GetUtf8JsonString(parentIndex, JsonTokenType.String);");
            ctx.AppendLine($"JsonSchemaEvaluation.{matchMethodName}(unescapedFormatString.Span, {formatKeywordLiteral}, ref context);");
            ctx.AppendLine();
            ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.PopIndent();
            ctx.AppendLine("}");

            if (!isContentFormat)
            {
                // For content keywords, the "else" IgnoredKeyword would be incorrectly
                // treated as an annotation by the annotation producer. Content annotations
                // are already gated by CoreTypes.String in EmitAnnotations.
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"context.IgnoredKeyword(JsonSchemaEvaluation.IgnoredNotTypeString, {formatKeywordLiteral});");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }
        else if (expectedTokenType == JsonTokenType.Number)
        {
            ctx.AppendLine("if (tokenType == JsonTokenType.Number)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("ReadOnlyMemory<byte> formatRawValue = parentDocument.GetRawSimpleValue(parentIndex);");
            ctx.AppendLine("JsonElementHelpers.TryParseNumber(formatRawValue.Span, out bool formatIsNeg, out ReadOnlySpan<byte> formatIntegral, out ReadOnlySpan<byte> formatFractional, out int formatExponent);");
            ctx.AppendLine($"JsonSchemaEvaluation.{matchMethodName}(formatIsNeg, formatIntegral, formatFractional, formatExponent, {formatKeywordLiteral}, ref context);");
            ctx.AppendLine();
            ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine("else");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"context.IgnoredKeyword(JsonSchemaEvaluation.IgnoredNotTypeNumber, {formatKeywordLiteral});");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }
    }

    private static string? GetFormatMatchMethodName(string format)
    {
        return format switch
        {
            // String formats
            "date" => "MatchDate",
            "date-time" => "MatchDateTime",
            "time" => "MatchTime",
            "duration" => "MatchDuration",
            "email" => "MatchEmail",
            "idn-email" => "MatchIdnEmail",
            "hostname" => "MatchHostname",
            "idn-hostname" => "MatchIdnHostname",
            "ipv4" => "MatchIPV4",
            "ipv6" => "MatchIPV6",
            "uuid" => "MatchUuid",
            "uri" => "MatchUri",
            "uri-template" => "MatchUriTemplate",
            "uri-reference" => "MatchUriReference",
            "iri" => "MatchIri",
            "iri-reference" => "MatchIriReference",
            "json-pointer" => "MatchJsonPointer",
            "relative-json-pointer" => "MatchRelativeJsonPointer",
            "regex" => "MatchRegex",
            "corvus-base64-content-pre201909" => "MatchBase64Content",
            "corvus-base64-string-pre201909" => "MatchBase64String",
            "corvus-json-content-pre201909" => "MatchJsonContent",
            "corvus-base64-content" => "MatchBase64Content",
            "corvus-base64-string" => "MatchBase64String",
            "corvus-json-content" => "MatchJsonContent",

            // Number formats
            "byte" => "MatchByte",
            "sbyte" => "MatchSByte",
            "int16" => "MatchInt16",
            "int32" => "MatchInt32",
            "int64" => "MatchInt64",
            "int128" => "MatchInt128",
            "uint16" => "MatchUInt16",
            "uint32" => "MatchUInt32",
            "uint64" => "MatchUInt64",
            "uint128" => "MatchUInt128",
            "half" => "MatchHalf",
            "single" => "MatchSingle",
            "double" => "MatchDouble",
            "decimal" => "MatchDecimal",

            _ => null,
        };
    }

    private static void EmitCompositionValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        EmitAllOfValidation(ctx, typeDeclaration, subschemas);
        EmitAnyOfValidation(ctx, typeDeclaration, subschemas);
        EmitOneOfValidation(ctx, typeDeclaration, subschemas);
        EmitNotValidation(ctx, typeDeclaration, subschemas);
    }

    private static void EmitIfThenElseValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        SingleSubschemaKeywordTypeDeclaration? ifType = typeDeclaration.IfSubschemaType();
        if (ifType is null)
        {
            return;
        }

        SingleSubschemaKeywordTypeDeclaration? thenType = typeDeclaration.ThenSubschemaType();
        SingleSubschemaKeywordTypeDeclaration? elseType = typeDeclaration.ElseSubschemaType();

        string ifLoc = ifType.UnreducedType.LocatedSchema.Location.ToString();
        if (!subschemas.TryGetValue(ifLoc, out SubschemaInfo? ifInfo))
        {
            return;
        }

        string ifPathField = ifInfo.PathFieldName ?? "null";
        string ifSchemaPathField = ifInfo.SchemaPathFieldName ?? "null";
        string ifFormattedKeyword = FormatUtf8Literal(ifType.Keyword.Keyword);

        bool ifUseItems = ifType.UnreducedType.RequiresItemsEvaluationTracking();
        bool ifUseProps = ifType.UnreducedType.RequiresPropertyEvaluationTracking();

        ctx.AppendLine();
        ctx.AppendLine("JsonSchemaContext ifContext =");
        ctx.PushIndent();
        ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(ifUseItems)}, useEvaluatedProperties: {BoolLiteral(ifUseProps)}, evaluationPath: {ifPathField}, schemaEvaluationPath: {ifSchemaPathField});");
        ctx.PopIndent();
        ctx.AppendLine($"{ifInfo.MethodName}(parentDocument, parentIndex, ref ifContext);");
        ctx.AppendLine();
        ctx.AppendLine("if (ifContext.IsMatch)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("context.ApplyEvaluated(ref ifContext);");

        if (thenType is not null)
        {
            string thenLoc = thenType.UnreducedType.LocatedSchema.Location.ToString();
            if (subschemas.TryGetValue(thenLoc, out SubschemaInfo? thenInfo))
            {
                string thenPathField = thenInfo.PathFieldName ?? "null";
                string thenSchemaPathField = thenInfo.SchemaPathFieldName ?? "null";
                string thenFormattedKeyword = FormatUtf8Literal(thenType.Keyword.Keyword);

                bool thenUseItems = thenType.UnreducedType.RequiresItemsEvaluationTracking();
                bool thenUseProps = thenType.UnreducedType.RequiresPropertyEvaluationTracking();

                ctx.AppendLine();
                ctx.AppendLine("JsonSchemaContext thenContext =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(thenUseItems)}, useEvaluatedProperties: {BoolLiteral(thenUseProps)}, evaluationPath: {thenPathField}, schemaEvaluationPath: {thenSchemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{thenInfo.MethodName}(parentDocument, parentIndex, ref thenContext);");
                ctx.AppendLine();
                ctx.AppendLine("if (thenContext.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.ApplyEvaluated(ref thenContext);");
                ctx.AppendLine("context.CommitChildContext(true, ref thenContext);");
                ctx.AppendLine("context.CommitChildContext(true, ref ifContext);");
                ctx.AppendLine($"context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedThen, {thenFormattedKeyword});");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.PopChildContext(ref thenContext);");
                ctx.AppendLine("context.PopChildContext(ref ifContext);");
                ctx.AppendLine($"context.EvaluatedKeyword(false, JsonSchemaEvaluation.DidNotMatchThen, {thenFormattedKeyword});");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }
        else
        {
            ctx.AppendLine("context.CommitChildContext(true, ref ifContext);");
        }

        ctx.AppendLine($"context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedIfForThen, {ifFormattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();

        // Pop ifContext first — PopChildContextUnsafe rolls back the committed result stack
        // to the point when the context was created, which would undo any commits made by
        // the else context if we waited to pop after committing else.
        ctx.AppendLine("context.PopChildContext(ref ifContext);");

        if (elseType is not null)
        {
            string elseLoc = elseType.UnreducedType.LocatedSchema.Location.ToString();
            if (subschemas.TryGetValue(elseLoc, out SubschemaInfo? elseInfo))
            {
                string elsePathField = elseInfo.PathFieldName ?? "null";
                string elseSchemaPathField = elseInfo.SchemaPathFieldName ?? "null";
                string elseFormattedKeyword = FormatUtf8Literal(elseType.Keyword.Keyword);

                bool elseUseItems = elseType.UnreducedType.RequiresItemsEvaluationTracking();
                bool elseUseProps = elseType.UnreducedType.RequiresPropertyEvaluationTracking();

                ctx.AppendLine();
                ctx.AppendLine("JsonSchemaContext elseContext =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(elseUseItems)}, useEvaluatedProperties: {BoolLiteral(elseUseProps)}, evaluationPath: {elsePathField}, schemaEvaluationPath: {elseSchemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{elseInfo.MethodName}(parentDocument, parentIndex, ref elseContext);");
                ctx.AppendLine();
                ctx.AppendLine("if (elseContext.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.ApplyEvaluated(ref elseContext);");
                ctx.AppendLine("context.CommitChildContext(true, ref elseContext);");
                ctx.AppendLine($"context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedElse, {elseFormattedKeyword});");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.PopChildContext(ref elseContext);");
                ctx.AppendLine($"context.EvaluatedKeyword(false, JsonSchemaEvaluation.DidNotMatchElse, {elseFormattedKeyword});");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }

        ctx.AppendLine($"context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedIfForElse, {ifFormattedKeyword});");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitObjectValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        PropertyMatcherInfo? matcherInfo)
    {
        // Collect local named properties with subschema validation.
        var properties = new List<(PropertyDeclaration Property, SubschemaInfo Info)>();
        foreach (PropertyDeclaration prop in typeDeclaration.PropertyDeclarations)
        {
            if (prop.LocalOrComposed == LocalOrComposed.Local && prop.Keyword is IObjectPropertyValidationKeyword)
            {
                string subLoc = prop.UnreducedPropertyType.LocatedSchema.Location.ToString();
                if (subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
                {
                    properties.Add((prop, info));
                }
            }
        }

        // Collect property count keywords.
        var propCountKeywords = typeDeclaration.Keywords().OfType<IPropertyCountConstantValidationKeyword>().ToList();

        // Collect required properties (only from our named properties list).
        var requiredProperties = new List<(PropertyDeclaration Property, SubschemaInfo Info)>();
        foreach (var entry in properties)
        {
            if (entry.Property.RequiredOrOptional == RequiredOrOptional.Required)
            {
                requiredProperties.Add(entry);
            }
        }

        // Collect required-only properties (required but not in properties schema).
        var requiredOnlyProperties = new List<PropertyDeclaration>();
        foreach (PropertyDeclaration prop in typeDeclaration.PropertyDeclarations)
        {
            if (prop.LocalOrComposed == LocalOrComposed.Local &&
                prop.RequiredOrOptional == RequiredOrOptional.Required &&
                !(prop.Keyword is IObjectPropertyValidationKeyword))
            {
                requiredOnlyProperties.Add(prop);
            }
        }

        // Collect dependent required declarations (dependentRequired and property-dependency part of dependencies).
        var dependentRequiredDeclarations = new List<DependentRequiredDeclaration>();
        if (typeDeclaration.DependentRequired() is { } depReqDict)
        {
            foreach (IReadOnlyCollection<DependentRequiredDeclaration> depReqCollection in depReqDict.Values)
            {
                dependentRequiredDeclarations.AddRange(depReqCollection);
            }
        }

        // Build a unique set of property names for dependent required tracking.
        var depReqPropertyNames = new List<string>();
        var depReqPropertyIndices = new Dictionary<string, int>();
        foreach (DependentRequiredDeclaration decl in dependentRequiredDeclarations)
        {
            if (!depReqPropertyIndices.ContainsKey(decl.JsonPropertyName))
            {
                depReqPropertyIndices[decl.JsonPropertyName] = depReqPropertyNames.Count;
                depReqPropertyNames.Add(decl.JsonPropertyName);
            }

            foreach (string dep in decl.Dependencies)
            {
                if (!depReqPropertyIndices.ContainsKey(dep))
                {
                    depReqPropertyIndices[dep] = depReqPropertyNames.Count;
                    depReqPropertyNames.Add(dep);
                }
            }
        }

        // Collect additionalProperties fallback (plain fallback only — not evaluated-tracking keywords).
        // Both additionalProperties (ILocalEvaluatedPropertyValidationKeyword) and unevaluatedProperties
        // (ILocalAndAppliedEvaluatedPropertyValidationKeyword) are handled by EmitUnevaluatedProperties.
        SubschemaInfo? additionalPropertiesInfo = null;
        foreach (IFallbackObjectPropertyTypeProviderKeyword keyword in typeDeclaration.Keywords().OfType<IFallbackObjectPropertyTypeProviderKeyword>())
        {
            if (keyword is ILocalEvaluatedPropertyValidationKeyword or ILocalAndAppliedEvaluatedPropertyValidationKeyword)
            {
                continue;
            }

            if (keyword.TryGetFallbackObjectPropertyType(typeDeclaration, out FallbackObjectPropertyType? fallbackType) &&
                fallbackType is FallbackObjectPropertyType fallback)
            {
                string subLoc = fallback.UnreducedType.LocatedSchema.Location.ToString();
                subschemas.TryGetValue(subLoc, out additionalPropertiesInfo);
            }
        }

        // Collect propertyNames subschema.
        SubschemaInfo? propertyNamesInfo = null;
        foreach (IObjectPropertyNameSubschemaValidationKeyword keyword in typeDeclaration.Keywords().OfType<IObjectPropertyNameSubschemaValidationKeyword>())
        {
            if (keyword.TryGetPropertyNameDeclaration(typeDeclaration, out TypeDeclaration? propertyNamesType) &&
                propertyNamesType is not null)
            {
                string subLoc = propertyNamesType.LocatedSchema.Location.ToString();
                subschemas.TryGetValue(subLoc, out propertyNamesInfo);
            }
        }

        // Collect patternProperties.
        var patternProperties = new List<(PatternPropertyDeclaration PatternProp, SubschemaInfo Info)>();
        if (typeDeclaration.PatternProperties() is { } patternPropsDict)
        {
            foreach (IReadOnlyCollection<PatternPropertyDeclaration> patternPropertyCollection in patternPropsDict.Values)
            {
                foreach (PatternPropertyDeclaration patternProp in patternPropertyCollection)
                {
                    string subLoc = patternProp.UnreducedPatternPropertyType.LocatedSchema.Location.ToString();
                    if (subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
                    {
                        patternProperties.Add((patternProp, info));
                    }
                }
            }
        }

        // Collect dependentSchemas.
        var dependentSchemas = new List<(DependentSchemaDeclaration DepSchema, SubschemaInfo Info)>();
        if (typeDeclaration.DependentSchemasSubschemaTypes() is { } depSchemasDict)
        {
            foreach (IReadOnlyCollection<DependentSchemaDeclaration> depSchemaCollection in depSchemasDict.Values)
            {
                foreach (DependentSchemaDeclaration depSchema in depSchemaCollection)
                {
                    string subLoc = depSchema.UnreducedDepdendentSchemaType.LocatedSchema.Location.ToString();
                    if (subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
                    {
                        dependentSchemas.Add((depSchema, info));
                    }
                }
            }
        }

        bool needsEnumeration = properties.Count > 0 || patternProperties.Count > 0 || additionalPropertiesInfo is not null || propertyNamesInfo is not null || dependentSchemas.Count > 0 || requiredOnlyProperties.Count > 0 || dependentRequiredDeclarations.Count > 0;
        bool needsPropertyCount = needsEnumeration || propCountKeywords.Count > 0 || typeDeclaration.RequiresPropertyCount();

        if (!needsEnumeration && propCountKeywords.Count == 0 && requiredProperties.Count == 0)
        {
            return;
        }

        ctx.AppendLine();
        ctx.AppendLine("if (tokenType == JsonTokenType.StartObject)");
        ctx.AppendLine("{");
        ctx.PushIndent();

        if (needsPropertyCount)
        {
            if (needsEnumeration)
            {
                ctx.AppendLine("int propertyCount = 0;");
            }
            else
            {
                ctx.AppendLine("int propertyCount = parentDocument.GetPropertyCount(parentIndex);");
            }
        }

        // Required property bitmask (includes both property-schema required and required-only).
        int totalRequiredCount = requiredProperties.Count + requiredOnlyProperties.Count;
        int requiredIntCount = totalRequiredCount > 0 ? (int)System.Math.Ceiling(totalRequiredCount / 32.0) : 0;
        if (totalRequiredCount > 0)
        {
            ctx.AppendLine($"Span<uint> requiredBits = stackalloc uint[{requiredIntCount}];");
        }
        else if (matcherInfo is not null)
        {
            // The property matcher delegate always receives requiredBits; declare an empty span
            // when there are no required properties.
            ctx.AppendLine("Span<uint> requiredBits = default;");
        }

        // DependentSchemas property tracking bitmask.
        if (dependentSchemas.Count > 0)
        {
            int depSchemaIntCount = (int)System.Math.Ceiling(dependentSchemas.Count / 32.0);
            ctx.AppendLine($"Span<uint> depSchemaBits = stackalloc uint[{depSchemaIntCount}];");
        }

        // DependentRequired property tracking bitmask.
        if (depReqPropertyNames.Count > 0)
        {
            int depReqIntCount = (int)System.Math.Ceiling(depReqPropertyNames.Count / 32.0);
            ctx.AppendLine($"Span<uint> depReqBits = stackalloc uint[{depReqIntCount}];");
        }

        if (needsEnumeration)
        {
            ctx.AppendLine();
            ctx.AppendLine("var enumerator = new ObjectEnumerator(parentDocument, parentIndex);");
            ctx.AppendLine("while (enumerator.MoveNext())");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("int currentIndex = enumerator.CurrentIndex;");
            ctx.AppendLine("using UnescapedUtf8JsonString unescapedPropertyName = parentDocument.GetPropertyNameUnescaped(currentIndex);");

            // Named property matching via hash-based PropertySchemaMatchers or SequenceEqual fallback.
            // Determine if we can use an else clause to skip pattern/additional checks for matched properties.
            bool useElseClause = matcherInfo is not null
                && (patternProperties.Count > 0 || additionalPropertiesInfo is not null)
                && propertyNamesInfo is null
                && !AnyNamedPropertyMatchesPatternProperty(properties, patternProperties);

            if (matcherInfo is not null)
            {
                string delegateName = matcherInfo.DelegateTypeName;
                string tryGetMethod = matcherInfo.TryGetMethodName;

                ctx.AppendLine();
                ctx.AppendLine($"if ({tryGetMethod}(unescapedPropertyName.Span, out {delegateName}? validator))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("validator!(parentDocument, currentIndex, propertyCount, ref context, requiredBits);");
                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.PopIndent();
                ctx.AppendLine("}");

                if (useElseClause)
                {
                    ctx.AppendLine("else");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                }
            }

            // patternProperties matching.
            if (patternProperties.Count > 0)
            {
                // patternProperties are checked for EVERY property (not in an else block),
                // because a property can match both properties and patternProperties.
                for (int i = 0; i < patternProperties.Count; i++)
                {
                    PatternPropertyDeclaration patternProp = patternProperties[i].PatternProp;
                    SubschemaInfo info = patternProperties[i].Info;
                    string pathField = info.PathFieldName ?? "null";
                    string schemaPathField = info.SchemaPathFieldName ?? "null";
                    string patternCtxVar = $"patternCtx_{i}";
                    RegexPatternCategory category = CodeGenerationExtensions.ClassifyRegexPattern(patternProp.Pattern);

                    ctx.AppendLine();

                    if (category == RegexPatternCategory.Noop)
                    {
                        // Noop patterns always match — emit body without an if-guard.
                        string quotedPattern = SymbolDisplay.FormatLiteral(patternProp.Pattern, true);
                        ctx.AppendLine($"// Pattern {quotedPattern} always matches.");
                    }
                    else
                    {
                        string condition = BuildPatternPropertyCondition(patternProp, category);
                        ctx.AppendLine($"if ({condition})");
                        ctx.AppendLine("{");
                        ctx.PushIndent();
                    }

                    ctx.AppendLine("context.AddLocalEvaluatedProperty(propertyCount);");
                    ctx.AppendLine($"JsonSchemaContext {patternCtxVar} =");
                    ctx.PushIndent();
                    ctx.AppendLine($"context.PushChildContextUnescaped(parentDocument, currentIndex, useEvaluatedItems: {BoolLiteral(info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(info.UseEvaluatedProperties)}, unescapedPropertyName.Span, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
                    ctx.PopIndent();
                    ctx.AppendLine($"{info.MethodName}(parentDocument, currentIndex, ref {patternCtxVar});");
                    ctx.AppendLine($"context.CommitChildContext({patternCtxVar}.IsMatch, ref {patternCtxVar});");
                    ctx.AppendLine();
                    ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("return;");
                    ctx.PopIndent();
                    ctx.AppendLine("}");

                    if (category != RegexPatternCategory.Noop)
                    {
                        ctx.PopIndent();
                        ctx.AppendLine("}");
                    }
                }
            }

            // additionalProperties fallback — only for properties not matched by properties or patternProperties.
            if (additionalPropertiesInfo is not null)
            {
                string apPathField = additionalPropertiesInfo.PathFieldName ?? "null";
                string apSchemaPathField = additionalPropertiesInfo.SchemaPathFieldName ?? "null";

                ctx.AppendLine();

                // Check if property was already evaluated by properties or patternProperties.
                if (properties.Count > 0 || patternProperties.Count > 0)
                {
                    ctx.AppendLine("if (!context.HasLocalEvaluatedProperty(propertyCount))");
                }

                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.AddLocalEvaluatedProperty(propertyCount);");
                ctx.AppendLine("JsonSchemaContext additionalCtx =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContextUnescaped(parentDocument, currentIndex, useEvaluatedItems: {BoolLiteral(additionalPropertiesInfo.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(additionalPropertiesInfo.UseEvaluatedProperties)}, unescapedPropertyName.Span, evaluationPath: {apPathField}, schemaEvaluationPath: {apSchemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{additionalPropertiesInfo.MethodName}(parentDocument, currentIndex, ref additionalCtx);");
                ctx.AppendLine("context.CommitChildContext(additionalCtx.IsMatch, ref additionalCtx);");
                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            if (useElseClause)
            {
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            // propertyNames validation.
            if (propertyNamesInfo is not null)
            {
                string pnPathField = propertyNamesInfo.PathFieldName ?? "null";
                string pnSchemaPathField = propertyNamesInfo.SchemaPathFieldName ?? "null";

                ctx.AppendLine();
                ctx.AppendLine("using var propertyNameDoc = FixedStringJsonDocument<JsonElement>.Parse(parentDocument.GetPropertyNameRaw(currentIndex, true), parentDocument.ValueIsEscaped(currentIndex, true));");
                ctx.AppendLine("JsonSchemaContext propertyNameCtx =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(propertyNameDoc, 0, useEvaluatedItems: false, useEvaluatedProperties: false, evaluationPath: {pnPathField}, schemaEvaluationPath: {pnSchemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{propertyNamesInfo.MethodName}(propertyNameDoc, 0, ref propertyNameCtx);");
                ctx.AppendLine("if (!propertyNameCtx.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"context.EvaluatedKeyword(false, null, {FormatUtf8Literal("propertyNames")});");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("context.PopChildContext(ref propertyNameCtx);");
                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            // Track required-only properties (required but not in properties schema).
            for (int i = 0; i < requiredOnlyProperties.Count; i++)
            {
                PropertyDeclaration prop = requiredOnlyProperties[i];
                int bitIndex = requiredProperties.Count + i;
                int offset = bitIndex / 32;
                int bit = bitIndex % 32;
                string quotedPropName = SymbolDisplay.FormatLiteral(prop.JsonPropertyName, true);

                ctx.AppendLine();
                ctx.AppendLine($"if (unescapedPropertyName.Span.SequenceEqual({quotedPropName}u8))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"requiredBits[{offset}] |= 0x{(1u << bit):X8};");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            // Track dependent required property names.
            for (int i = 0; i < depReqPropertyNames.Count; i++)
            {
                string quotedName = SymbolDisplay.FormatLiteral(depReqPropertyNames[i], true);
                int offset = i / 32;
                int bit = i % 32;

                ctx.AppendLine();
                ctx.AppendLine($"if (unescapedPropertyName.Span.SequenceEqual({quotedName}u8))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"depReqBits[{offset}] |= 0x{(1u << bit):X8};");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            // Track dependent schema property names.
            if (dependentSchemas.Count > 0)
            {
                for (int i = 0; i < dependentSchemas.Count; i++)
                {
                    string quotedDepPropName = SymbolDisplay.FormatLiteral(dependentSchemas[i].DepSchema.JsonPropertyName, true);
                    int offset = i / 32;
                    int bit = i % 32;
                    ctx.AppendLine();
                    ctx.AppendLine($"if (unescapedPropertyName.Span.SequenceEqual({quotedDepPropName}u8))");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"depSchemaBits[{offset}] |= 0x{(1u << bit):X8};");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                }
            }

            ctx.AppendLine();
            ctx.AppendLine("propertyCount++;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        // Evaluate dependent schemas for properties that were found.
        if (dependentSchemas.Count > 0)
        {
            for (int i = 0; i < dependentSchemas.Count; i++)
            {
                DependentSchemaDeclaration depSchema = dependentSchemas[i].DepSchema;
                SubschemaInfo info = dependentSchemas[i].Info;
                string pathField = info.PathFieldName ?? "null";
                string schemaPathField = info.SchemaPathFieldName ?? "null";
                string depCtxVar = $"depSchemaCtx_{i}";
                int offset = i / 32;
                int bit = i % 32;

                ctx.AppendLine();
                ctx.AppendLine($"if ((depSchemaBits[{offset}] & 0x{(1u << bit):X8}) != 0)");
                ctx.AppendLine("{");
                ctx.PushIndent();

                bool useProps = typeDeclaration.RequiresPropertyEvaluationTracking();
                bool useItems = typeDeclaration.RequiresItemsEvaluationTracking();
                ctx.AppendLine($"JsonSchemaContext {depCtxVar} =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(useItems)}, useEvaluatedProperties: {BoolLiteral(useProps)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref {depCtxVar});");
                ctx.AppendLine($"if (!{depCtxVar}.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"context.CommitChildContext(false, ref {depCtxVar});");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"context.ApplyEvaluated(ref {depCtxVar});");
                ctx.AppendLine($"context.CommitChildContext(true, ref {depCtxVar});");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }

        // Property count validation.
        foreach (IPropertyCountConstantValidationKeyword keyword in propCountKeywords)
        {
            if (!keyword.TryGetOperator(typeDeclaration, out Operator op) || op == Operator.None)
            {
                continue;
            }

            if (!keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants) || constants.Length == 0)
            {
                continue;
            }

            int rawValue = (int)constants[0].GetDecimal();
            string expected = rawValue.ToString();
            string opFunc = GetPropertyCountOperatorFunction(op);

            ctx.AppendLine();
            ctx.AppendLine($"{opFunc}({expected}, propertyCount, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
            ctx.AppendLine();
            ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        // Required property validation.
        for (int i = 0; i < requiredProperties.Count; i++)
        {
            PropertyDeclaration prop = requiredProperties[i].Property;
            int offset = i / 32;
            int bit = i % 32;
            string mask = $"0x{(1u << bit):X8}";
            string quotedPropName = SymbolDisplay.FormatLiteral(prop.JsonPropertyName, true);
            string keywordLiteral = prop.RequiredKeyword is IKeyword reqKw ? FormatUtf8Literal(reqKw.Keyword) : FormatUtf8Literal("required");

            ctx.AppendLine();
            ctx.AppendLine($"if ((requiredBits[{offset}] & {mask}) == 0)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"context.EvaluatedKeywordForProperty(false, messageProvider: null, {quotedPropName}u8, {keywordLiteral});");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        // Required-only property validation.
        for (int i = 0; i < requiredOnlyProperties.Count; i++)
        {
            PropertyDeclaration prop = requiredOnlyProperties[i];
            int bitIndex = requiredProperties.Count + i;
            int offset = bitIndex / 32;
            int bit = bitIndex % 32;
            string mask = $"0x{(1u << bit):X8}";
            string quotedPropName = SymbolDisplay.FormatLiteral(prop.JsonPropertyName, true);
            string keywordLiteral = prop.RequiredKeyword is IKeyword reqKw ? FormatUtf8Literal(reqKw.Keyword) : FormatUtf8Literal("required");

            ctx.AppendLine();
            ctx.AppendLine($"if ((requiredBits[{offset}] & {mask}) == 0)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"context.EvaluatedKeywordForProperty(false, messageProvider: null, {quotedPropName}u8, {keywordLiteral});");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        // Dependent required property validation.
        if (dependentRequiredDeclarations.Count > 0)
        {
            foreach (DependentRequiredDeclaration decl in dependentRequiredDeclarations)
            {
                int triggerIndex = depReqPropertyIndices[decl.JsonPropertyName];
                int triggerOffset = triggerIndex / 32;
                int triggerBit = triggerIndex % 32;
                string triggerMask = $"0x{(1u << triggerBit):X8}";
                string keywordLiteral = FormatUtf8Literal(((IKeyword)decl.Keyword).Keyword);

                ctx.AppendLine();
                ctx.AppendLine($"if ((depReqBits[{triggerOffset}] & {triggerMask}) != 0)");
                ctx.AppendLine("{");
                ctx.PushIndent();

                foreach (string dep in decl.Dependencies)
                {
                    int depIndex = depReqPropertyIndices[dep];
                    int depOffset = depIndex / 32;
                    int depBit = depIndex % 32;
                    string depMask = $"0x{(1u << depBit):X8}";
                    string quotedDepName = SymbolDisplay.FormatLiteral(dep, true);

                    ctx.AppendLine($"if ((depReqBits[{depOffset}] & {depMask}) == 0)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"context.EvaluatedKeywordForProperty(false, messageProvider: null, {quotedDepName}u8, {keywordLiteral});");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                }

                ctx.PopIndent();
                ctx.AppendLine("}");
            }
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static string GetPropertyCountOperatorFunction(Operator op)
    {
        return op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchPropertyCountEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchPropertyCountNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchPropertyCountLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchPropertyCountLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchPropertyCountGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchPropertyCountGreaterThanOrEquals",
            _ => throw new System.InvalidOperationException($"Unsupported property count operator: {op}"),
        };
    }

    private static void EmitArrayValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        // Collect tuple items (prefixItems).
        var tupleItems = new List<(ReducedTypeDeclaration Item, SubschemaInfo Info, int Index)>();
        string? tupleKeywordName = null;
        foreach (ITupleTypeProviderKeyword keyword in typeDeclaration.Keywords().OfType<ITupleTypeProviderKeyword>())
        {
            if (keyword.TryGetTupleType(typeDeclaration, out TupleTypeDeclaration? tupleType) &&
                tupleType is TupleTypeDeclaration tuple)
            {
                tupleKeywordName = ((IKeyword)keyword).Keyword;
                for (int index = 0; index < tuple.ItemsTypes.Length; index++)
                {
                    ReducedTypeDeclaration item = tuple.ItemsTypes[index];
                    TypeDeclaration unreducedItem = tuple.UnreducedTypes[index];
                    string subLoc = unreducedItem.LocatedSchema.Location.ToString();
                    if (subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
                    {
                        tupleItems.Add((item, info, index));
                    }
                }
            }
        }

        // Collect non-tuple items.
        SubschemaInfo? itemsInfo = null;
        foreach (IArrayItemsTypeProviderKeyword keyword in typeDeclaration.Keywords().OfType<IArrayItemsTypeProviderKeyword>())
        {
            if (keyword is IKeyword kw && kw.Keyword == "unevaluatedItems")
            {
                continue;
            }

            if (keyword.TryGetArrayItemsType(typeDeclaration, out ArrayItemsTypeDeclaration? itemsType) &&
                itemsType is ArrayItemsTypeDeclaration items)
            {
                string subLoc = items.UnreducedType.LocatedSchema.Location.ToString();
                subschemas.TryGetValue(subLoc, out itemsInfo);
            }
        }

        // Collect contains.
        SubschemaInfo? containsInfo = null;
        string? containsKeywordName = null;
        var containsOperators = new List<(int Value, Operator Op, string Keyword)>();
        foreach (IArrayContainsValidationKeyword keyword in typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>())
        {
            containsKeywordName = keyword.Keyword;
            if (keyword.TryGetContainsItemType(typeDeclaration, out ArrayItemsTypeDeclaration? containsType) &&
                containsType is ArrayItemsTypeDeclaration contains)
            {
                string subLoc = contains.UnreducedType.LocatedSchema.Location.ToString();
                subschemas.TryGetValue(subLoc, out containsInfo);
            }
        }

        // Collect contains count operators (minContains/maxContains).
        foreach (IArrayContainsCountConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IArrayContainsCountConstantValidationKeyword>())
        {
            if (keyword.TryGetOperator(typeDeclaration, out Operator op) &&
                keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants) &&
                constants is { Length: > 0 })
            {
                int value = (int)constants[0].GetDecimal();
                containsOperators.Add((value, op, keyword.Keyword));
            }
        }

        // If contains is present but no explicit GreaterThan/GreaterThanOrEquals, add default > 0.
        if (containsInfo is not null &&
            !containsOperators.Any(c => c.Op is Operator.GreaterThan or Operator.GreaterThanOrEquals))
        {
            containsOperators.Add((0, Operator.GreaterThan, containsKeywordName ?? "contains"));
        }

        // Collect uniqueItems.
        bool requiresUniqueItems = typeDeclaration.Keywords().OfType<IUniqueItemsArrayValidationKeyword>()
            .Any(k => k.RequiresUniqueItems(typeDeclaration));
        string? uniqueItemsKeyword = requiresUniqueItems
            ? typeDeclaration.Keywords().OfType<IUniqueItemsArrayValidationKeyword>().First(k => k.RequiresUniqueItems(typeDeclaration)).Keyword
            : null;

        // Collect item count keywords (minItems/maxItems).
        var itemCountKeywords = typeDeclaration.Keywords().OfType<IArrayLengthConstantValidationKeyword>().ToList();

        bool needsEnumeration = tupleItems.Count > 0 || itemsInfo is not null || containsInfo is not null || requiresUniqueItems;
        bool needsItemCount = needsEnumeration || itemCountKeywords.Count > 0 || typeDeclaration.RequiresArrayLength();

        if (!needsEnumeration && itemCountKeywords.Count == 0)
        {
            return;
        }

        ctx.AppendLine();
        ctx.AppendLine("if (tokenType == JsonTokenType.StartArray)");
        ctx.AppendLine("{");
        ctx.PushIndent();

        if (needsItemCount)
        {
            if (needsEnumeration)
            {
                ctx.AppendLine("int itemCount = 0;");
            }
            else
            {
                ctx.AppendLine("int itemCount = parentDocument.GetArrayLength(parentIndex);");
            }
        }

        if (containsInfo is not null)
        {
            ctx.AppendLine("int containsCount = 0;");
        }

        if (requiresUniqueItems)
        {
            ctx.AppendLine("bool hasUniqueItems = true;");
            ctx.AppendLine("using UniqueItemsHashSet uniqueItemsHashSet = new UniqueItemsHashSet(parentDocument, parentDocument.GetArrayLength(parentIndex), stackalloc int[UniqueItemsHashSet.StackAllocBucketSize], stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]);");
        }

        if (needsEnumeration)
        {
            ctx.AppendLine();
            ctx.AppendLine("var enumerator = new ArrayEnumerator(parentDocument, parentIndex);");
            ctx.AppendLine("while (enumerator.MoveNext())");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("int currentIndex = enumerator.CurrentIndex;");

            // Contains validation (before tuple/items so it evaluates every item).
            if (containsInfo is not null)
            {
                string containsPathField = containsInfo.PathFieldName ?? "null";
                string containsSchemaPathField = containsInfo.SchemaPathFieldName ?? "null";

                ctx.AppendLine();
                ctx.AppendLine("JsonSchemaContext containsCtx =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, currentIndex, useEvaluatedItems: {BoolLiteral(containsInfo.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(containsInfo.UseEvaluatedProperties)}, itemCount, evaluationPath: {containsPathField}, schemaEvaluationPath: {containsSchemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{containsInfo.MethodName}(parentDocument, currentIndex, ref containsCtx);");
                ctx.AppendLine("if (containsCtx.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("containsCount++;");
                ctx.AppendLine("context.CommitChildContext(true, ref containsCtx);");
                ctx.AppendLine("context.AddLocalEvaluatedItem(itemCount);");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.PopChildContext(ref containsCtx);");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            // UniqueItems check per item.
            if (requiresUniqueItems)
            {
                ctx.AppendLine();
                ctx.AppendLine("if (hasUniqueItems && !uniqueItemsHashSet.AddItemIfNotExists(currentIndex))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("hasUniqueItems = false;");
                ctx.AppendLine($"context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.ExpectedUniqueItems, {FormatUtf8Literal(uniqueItemsKeyword!)});");
                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            // Tuple items and/or non-tuple items.
            if (tupleItems.Count > 0)
            {
                ctx.AppendLine();
                ctx.AppendLine("switch (itemCount)");
                ctx.AppendLine("{");
                ctx.PushIndent();

                foreach (var (item, info, tupleIndex) in tupleItems)
                {
                    string pathField = info.PathFieldName ?? "null";
                    string schemaPathField = info.SchemaPathFieldName ?? "null";
                    string caseCtxVar = $"tupleCtx_{tupleIndex}";

                    ctx.AppendLine($"case {tupleIndex}:");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"JsonSchemaContext {caseCtxVar} =");
                    ctx.PushIndent();
                    ctx.AppendLine($"context.PushChildContext(parentDocument, currentIndex, useEvaluatedItems: {BoolLiteral(info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(info.UseEvaluatedProperties)}, itemCount, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
                    ctx.PopIndent();
                    ctx.AppendLine($"{info.MethodName}(parentDocument, currentIndex, ref {caseCtxVar});");
                    ctx.AppendLine($"if (!{caseCtxVar}.IsMatch)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"context.CommitChildContext(false, ref {caseCtxVar});");
                    ctx.AppendLine();
                    ctx.AppendLine("if (!context.HasCollector)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("return;");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.AppendLine("else");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"context.CommitChildContext(true, ref {caseCtxVar});");
                    ctx.AppendLine("context.AddLocalEvaluatedItem(itemCount);");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.AppendLine();
                    ctx.AppendLine("break;");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                }

                // Default case for non-tuple items (items after prefixItems).
                if (itemsInfo is not null)
                {
                    string itemsPathField = itemsInfo.PathFieldName ?? "null";
                    string itemsSchemaPathField = itemsInfo.SchemaPathFieldName ?? "null";

                    ctx.AppendLine("default:");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("JsonSchemaContext nonTupleCtx =");
                    ctx.PushIndent();
                    ctx.AppendLine($"context.PushChildContext(parentDocument, currentIndex, useEvaluatedItems: {BoolLiteral(itemsInfo.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(itemsInfo.UseEvaluatedProperties)}, itemCount, evaluationPath: {itemsPathField}, schemaEvaluationPath: {itemsSchemaPathField});");
                    ctx.PopIndent();
                    ctx.AppendLine($"{itemsInfo.MethodName}(parentDocument, currentIndex, ref nonTupleCtx);");
                    ctx.AppendLine("if (!nonTupleCtx.IsMatch)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("context.CommitChildContext(false, ref nonTupleCtx);");
                    ctx.AppendLine();
                    ctx.AppendLine("if (!context.HasCollector)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("return;");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.AppendLine("else");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine("context.CommitChildContext(true, ref nonTupleCtx);");
                    ctx.AppendLine("context.AddLocalEvaluatedItem(itemCount);");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.AppendLine();
                    ctx.AppendLine("break;");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                }

                ctx.PopIndent();
                ctx.AppendLine("}");
            }
            else if (itemsInfo is not null)
            {
                // No tuple, just items applied to every element.
                string itemsPathField = itemsInfo.PathFieldName ?? "null";
                string itemsSchemaPathField = itemsInfo.SchemaPathFieldName ?? "null";

                ctx.AppendLine();
                ctx.AppendLine("JsonSchemaContext itemCtx =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, currentIndex, useEvaluatedItems: {BoolLiteral(itemsInfo.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(itemsInfo.UseEvaluatedProperties)}, itemCount, evaluationPath: {itemsPathField}, schemaEvaluationPath: {itemsSchemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{itemsInfo.MethodName}(parentDocument, currentIndex, ref itemCtx);");
                ctx.AppendLine("if (!itemCtx.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.CommitChildContext(false, ref itemCtx);");
                ctx.AppendLine();
                ctx.AppendLine("if (!context.HasCollector)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("return;");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine("context.CommitChildContext(true, ref itemCtx);");
                ctx.AppendLine("context.AddLocalEvaluatedItem(itemCount);");
                ctx.PopIndent();
                ctx.AppendLine("}");
            }

            ctx.AppendLine();
            ctx.AppendLine("itemCount++;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        // Item count validation (minItems/maxItems).
        foreach (IArrayLengthConstantValidationKeyword keyword in itemCountKeywords)
        {
            if (!keyword.TryGetOperator(typeDeclaration, out Operator op) || op == Operator.None)
            {
                continue;
            }

            if (!keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants) || constants.Length == 0)
            {
                continue;
            }

            int rawValue = (int)constants[0].GetDecimal();
            string expected = rawValue.ToString();
            string opFunc = GetItemCountOperatorFunction(op);

            ctx.AppendLine();
            ctx.AppendLine($"{opFunc}({expected}, itemCount, {FormatUtf8Literal(keyword.Keyword)}, ref context);");
            ctx.AppendLine();
            ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        // Contains count validation.
        foreach (var (value, op, keyword) in containsOperators)
        {
            string opFunc = GetContainsCountOperatorFunction(op);
            ctx.AppendLine();
            ctx.AppendLine($"{opFunc}({value}, containsCount, {FormatUtf8Literal(keyword)}, ref context);");
            ctx.AppendLine();
            ctx.AppendLine("if (!context.HasCollector && !context.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
        }

        // UniqueItems final evaluation.
        if (requiresUniqueItems && uniqueItemsKeyword is not null)
        {
            ctx.AppendLine();
            ctx.AppendLine($"context.EvaluatedKeyword(hasUniqueItems, messageProvider: JsonSchemaEvaluation.ExpectedUniqueItems, {FormatUtf8Literal(uniqueItemsKeyword)});");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static string GetItemCountOperatorFunction(Operator op)
    {
        return op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchItemCountEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchItemCountNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchItemCountLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchItemCountLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchItemCountGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchItemCountGreaterThanOrEquals",
            _ => throw new System.InvalidOperationException($"Unsupported item count operator: {op}"),
        };
    }

    private static string GetContainsCountOperatorFunction(Operator op)
    {
        return op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchContainsCountEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchContainsCountNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchContainsCountLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchContainsCountLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchContainsCountGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchContainsCountGreaterThanOrEquals",
            _ => throw new System.InvalidOperationException($"Unsupported contains count operator: {op}"),
        };
    }

    private static void EmitUnevaluatedValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        EmitUnevaluatedProperties(ctx, typeDeclaration, subschemas);
        EmitUnevaluatedItems(ctx, typeDeclaration, subschemas);
    }

    private static void EmitUnevaluatedProperties(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        // Local-only evaluated property check.
        if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localEvalProp)
        {
            EmitUnevaluatedPropertyLoop(ctx, typeDeclaration, subschemas, localEvalProp, "HasLocalEvaluatedProperty");
        }

        // Local + applied evaluated property check (from composition applicators).
        if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedEvalProp)
        {
            EmitUnevaluatedPropertyLoop(ctx, typeDeclaration, subschemas, localAndAppliedEvalProp, "HasLocalOrAppliedEvaluatedProperty");
        }
    }

    private static void EmitUnevaluatedPropertyLoop(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas,
        FallbackObjectPropertyType fallbackProperty,
        string checkMethodName)
    {
        string subLoc = fallbackProperty.UnreducedType.LocatedSchema.Location.ToString();
        if (!subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
        {
            return;
        }

        string pathField = info.PathFieldName ?? "null";
        string schemaPathField = info.SchemaPathFieldName ?? "null";
        string keywordLiteral = FormatUtf8Literal(fallbackProperty.Keyword.Keyword);

        ctx.AppendLine();
        ctx.AppendLine("if (tokenType == JsonTokenType.StartObject)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("int unevalPropCount = 0;");
        ctx.AppendLine("var unevalPropEnumerator = new ObjectEnumerator(parentDocument, parentIndex);");
        ctx.AppendLine("while (unevalPropEnumerator.MoveNext())");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"if (!context.{checkMethodName}(unevalPropCount))");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("int unevalCurrentIndex = unevalPropEnumerator.CurrentIndex;");
        ctx.AppendLine("using UnescapedUtf8JsonString unevalPropertyName = parentDocument.GetPropertyNameUnescaped(unevalCurrentIndex);");
        ctx.AppendLine("JsonSchemaContext unevalPropCtx =");
        ctx.PushIndent();
        ctx.AppendLine($"context.PushChildContextUnescaped(parentDocument, unevalCurrentIndex, useEvaluatedItems: {BoolLiteral(info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(info.UseEvaluatedProperties)}, unevalPropertyName.Span, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
        ctx.PopIndent();
        ctx.AppendLine($"{info.MethodName}(parentDocument, unevalCurrentIndex, ref unevalPropCtx);");
        ctx.AppendLine("if (!unevalPropCtx.IsMatch)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("context.CommitChildContext(false, ref unevalPropCtx);");
        ctx.AppendLine($"context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.ExpectedPropertyMatchesFallbackSchema, {keywordLiteral});");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("context.CommitChildContext(true, ref unevalPropCtx);");
        ctx.AppendLine("context.AddLocalEvaluatedProperty(unevalPropCount);");
        ctx.AppendLine($"context.EvaluatedKeyword(true, messageProvider: JsonSchemaEvaluation.ExpectedPropertyMatchesFallbackSchema, {keywordLiteral});");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine();
        ctx.AppendLine("unevalPropCount++;");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitUnevaluatedItems(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        ArrayItemsTypeDeclaration? unevalItemsType = typeDeclaration.ExplicitUnevaluatedItemsType();
        if (unevalItemsType is null)
        {
            return;
        }

        string subLoc = unevalItemsType.UnreducedType.LocatedSchema.Location.ToString();
        if (!subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
        {
            return;
        }

        string pathField = info.PathFieldName ?? "null";
        string schemaPathField = info.SchemaPathFieldName ?? "null";
        string keywordLiteral = FormatUtf8Literal(((IKeyword)unevalItemsType.Keyword).Keyword);

        ctx.AppendLine();
        ctx.AppendLine("if (tokenType == JsonTokenType.StartArray)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("int unevalItemCount = 0;");
        ctx.AppendLine("var unevalItemEnumerator = new ArrayEnumerator(parentDocument, parentIndex);");
        ctx.AppendLine("while (unevalItemEnumerator.MoveNext())");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("if (!context.HasLocalOrAppliedEvaluatedItem(unevalItemCount))");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("int unevalCurrentIndex = unevalItemEnumerator.CurrentIndex;");
        ctx.AppendLine("JsonSchemaContext unevalItemCtx =");
        ctx.PushIndent();
        ctx.AppendLine($"context.PushChildContext(parentDocument, unevalCurrentIndex, useEvaluatedItems: {BoolLiteral(info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(info.UseEvaluatedProperties)}, unevalItemCount, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
        ctx.PopIndent();
        ctx.AppendLine($"{info.MethodName}(parentDocument, unevalCurrentIndex, ref unevalItemCtx);");
        ctx.AppendLine("if (!unevalItemCtx.IsMatch)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("context.CommitChildContext(false, ref unevalItemCtx);");
        ctx.AppendLine();
        ctx.AppendLine("if (!context.HasCollector)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("return;");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine("else");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine("context.CommitChildContext(true, ref unevalItemCtx);");
        ctx.AppendLine("context.AddLocalEvaluatedItem(unevalItemCount);");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.AppendLine();
        ctx.AppendLine("unevalItemCount++;");
        ctx.PopIndent();
        ctx.AppendLine("}");
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitAnnotations(GenerationContext ctx, TypeDeclaration typeDeclaration)
    {
        var annotations = new List<(string Keyword, string JsonValue, CoreTypes AppliesTo)>();

        // Collect annotations from known IAnnotationProducingKeyword instances.
        foreach (IKeyword keyword in typeDeclaration.Keywords())
        {
            if (keyword is IAnnotationProducingKeyword annKw &&
                annKw.TryGetAnnotationJsonValue(typeDeclaration, out string rawJsonValue) &&
                annKw.AnnotationPreconditionsMet(typeDeclaration))
            {
                annotations.Add((keyword.Keyword, rawJsonValue, annKw.AnnotationAppliesToCoreTypes(typeDeclaration)));
            }
        }

        // Collect annotations from unknown keywords (schema properties not in the vocabulary).
        if (typeDeclaration.LocatedSchema.Schema.ValueKind == JsonValueKind.Object)
        {
            var knownKeywordNames = new HashSet<string>(
                typeDeclaration.LocatedSchema.Vocabulary.Keywords.Select(k => k.Keyword),
                System.StringComparer.Ordinal);

            foreach (JsonProperty prop in typeDeclaration.LocatedSchema.Schema.EnumerateObject())
            {
                if (!knownKeywordNames.Contains(prop.Name))
                {
                    annotations.Add((prop.Name, prop.Value.GetRawText(), CoreTypes.None));
                }
            }
        }

        if (annotations.Count == 0)
        {
            return;
        }

        ctx.AppendLine();
        ctx.AppendLine("if (context.HasCollector)");
        ctx.AppendLine("{");
        ctx.PushIndent();

        foreach (var (keyword, jsonValue, appliesTo) in annotations)
        {
            string valueUtf8 = FormatUtf8Literal(jsonValue);
            string ignoredKeywordCall = $"context.IgnoredKeyword(static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage({valueUtf8}, buffer, out written), {FormatUtf8Literal(keyword)});";

            if (appliesTo != CoreTypes.None && appliesTo != (CoreTypes.Array | CoreTypes.Boolean | CoreTypes.Integer | CoreTypes.Null | CoreTypes.Number | CoreTypes.Object | CoreTypes.String))
            {
                // Gate the annotation by the instance type.
                string condition = GetTokenTypeCondition(appliesTo);
                ctx.AppendLine($"if ({condition})");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine(ignoredKeywordCall);
                ctx.PopIndent();
                ctx.AppendLine("}");
            }
            else
            {
                ctx.AppendLine(ignoredKeywordCall);
            }
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static string GetTokenTypeCondition(CoreTypes types)
    {
        var conditions = new List<string>();

        if ((types & CoreTypes.String) != 0)
        {
            conditions.Add("parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.String");
        }

        if ((types & CoreTypes.Object) != 0)
        {
            conditions.Add("parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.StartObject");
        }

        if ((types & CoreTypes.Array) != 0)
        {
            conditions.Add("parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.StartArray");
        }

        if ((types & CoreTypes.Number) != 0)
        {
            conditions.Add("parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.Number");
        }

        if ((types & CoreTypes.Boolean) != 0)
        {
            conditions.Add("parentDocument.GetJsonTokenType(parentIndex) is JsonTokenType.True or JsonTokenType.False");
        }

        if ((types & CoreTypes.Null) != 0)
        {
            conditions.Add("parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.Null");
        }

        return conditions.Count == 1 ? conditions[0] : string.Join(" || ", conditions.Select(c => $"({c})"));
    }

    private static void EmitAllOfValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        if (typeDeclaration.AllOfCompositionTypes() is not { } allOf)
        {
            return;
        }

        bool useItems = typeDeclaration.RequiresItemsEvaluationTracking();
        bool useProps = typeDeclaration.RequiresPropertyEvaluationTracking();
        int groupIdx = 0;

        foreach (var kvp in allOf)
        {
            string keywordName = kvp.Key.Keyword;
            List<SubschemaInfo> entries = ResolveSubschemaInfos(kvp.Value, subschemas);

            if (entries.Count == 0)
            {
                groupIdx++;
                continue;
            }

            // Optimization: when a $ref (IReferenceKeyword) resolves to a single target,
            // call the target method directly when there is no collector, skipping the
            // PushChildContext/CommitChildContext overhead on the hot validation-only path.
            if (kvp.Key is IReferenceKeyword && entries.Count == 1)
            {
                SubschemaInfo info = entries[0];
                bool childNeedsItems = useItems || info.UseEvaluatedItems;
                bool childNeedsProps = useProps || info.UseEvaluatedProperties;

                // We can only inline when the child doesn't require additional evaluation
                // tracking beyond what the parent already provides.
                if (childNeedsItems == useItems && childNeedsProps == useProps)
                {
                    string contextVar = $"allOfCtx{(groupIdx > 0 ? groupIdx.ToString() : string.Empty)}_0";
                    string pathField = info.PathFieldName ?? "null";
                    string schemaPathField = info.SchemaPathFieldName ?? "null";

                    ctx.AppendLine();
                    ctx.AppendLine("if (context.HasCollector)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();

                    ctx.AppendLine($"JsonSchemaContext {contextVar} =");
                    ctx.PushIndent();
                    ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(childNeedsItems)}, useEvaluatedProperties: {BoolLiteral(childNeedsProps)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
                    ctx.PopIndent();
                    ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref {contextVar});");
                    ctx.AppendLine($"context.ApplyEvaluated(ref {contextVar});");
                    ctx.AppendLine($"context.CommitChildContext({contextVar}.IsMatch, ref {contextVar});");
                    ctx.AppendLine($"context.EvaluatedKeyword(context.IsMatch, context.IsMatch ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, {FormatUtf8Literal(keywordName)});");

                    ctx.PopIndent();
                    ctx.AppendLine("}");
                    ctx.AppendLine("else");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref context);");
                    ctx.PopIndent();
                    ctx.AppendLine("}");

                    groupIdx++;
                    continue;
                }
            }

            string suffix = groupIdx > 0 ? groupIdx.ToString() : string.Empty;
            string composedVar = $"allOfComposedIsMatch{suffix}";
            string endLabel = $"allOfEnd{suffix}";
            bool needsLabel = entries.Count > 1;

            ctx.AppendLine();
            ctx.AppendLine($"bool {composedVar} = true;");

            for (int i = 0; i < entries.Count; i++)
            {
                SubschemaInfo info = entries[i];
                string contextVar = $"allOfCtx{suffix}_{i}";
                string pathField = info.PathFieldName ?? "null";
                string schemaPathField = info.SchemaPathFieldName ?? "null";

                ctx.AppendLine();
                ctx.AppendLine($"JsonSchemaContext {contextVar} =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(useItems || info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(useProps || info.UseEvaluatedProperties)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref {contextVar});");
                ctx.AppendLine($"{composedVar} = {composedVar} && {contextVar}.IsMatch;");
                ctx.AppendLine($"context.ApplyEvaluated(ref {contextVar});");
                ctx.AppendLine($"context.CommitChildContext({contextVar}.IsMatch, ref {contextVar});");

                if (needsLabel && i < entries.Count - 1)
                {
                    ctx.AppendLine();
                    ctx.AppendLine($"if (!context.HasCollector && !{composedVar})");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"goto {endLabel};");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                }
            }

            if (needsLabel)
            {
                ctx.AppendLine();
                ctx.AppendLine($"{endLabel}:");
            }

            ctx.AppendLine($"context.EvaluatedKeyword({composedVar}, {composedVar} ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, {FormatUtf8Literal(keywordName)});");

            groupIdx++;
        }
    }

    private static void EmitAnyOfValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        if (typeDeclaration.AnyOfCompositionTypes() is not { } anyOf)
        {
            return;
        }

        bool useItems = typeDeclaration.RequiresItemsEvaluationTracking();
        bool useProps = typeDeclaration.RequiresPropertyEvaluationTracking();
        int groupIdx = 0;

        foreach (var kvp in anyOf)
        {
            string keywordName = kvp.Key.Keyword;
            List<SubschemaInfo> entries = ResolveSubschemaInfos(kvp.Value, subschemas);

            if (entries.Count == 0)
            {
                groupIdx++;
                continue;
            }

            string suffix = groupIdx > 0 ? groupIdx.ToString() : string.Empty;
            string composedVar = $"anyOfComposedIsMatch{suffix}";
            string endLabel = $"anyOfEnd{suffix}";
            bool needsLabel = entries.Count > 1;

            // Try anyOf discriminator fast path
            if (CodeGenerationExtensions.TryGetOneOfDiscriminator(
                    kvp.Value,
                    out string? discriminatorPropertyName,
                    out List<(string Value, int BranchIndex)>? discriminatorValues,
                    out JsonValueKind discriminatorValueKind,
                    requireRequired: false,
                    allowPartial: true))
            {
                EmitAnyOfDiscriminatorFastPath(
                    ctx, discriminatorPropertyName, discriminatorValues, discriminatorValueKind,
                    entries, useItems, useProps, suffix, keywordName);
            }

            // Sequential evaluation path (used when collector is present, or no discriminator)
            ctx.AppendLine();
            ctx.AppendLine($"bool {composedVar} = false;");

            for (int i = 0; i < entries.Count; i++)
            {
                SubschemaInfo info = entries[i];
                string contextVar = $"anyOfCtx{suffix}_{i}";
                string pathField = info.PathFieldName ?? "null";
                string schemaPathField = info.SchemaPathFieldName ?? "null";

                ctx.AppendLine();
                ctx.AppendLine($"JsonSchemaContext {contextVar} =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(useItems || info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(useProps || info.UseEvaluatedProperties)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref {contextVar});");
                ctx.AppendLine($"{composedVar} = {composedVar} || {contextVar}.IsMatch;");
                ctx.AppendLine($"if ({contextVar}.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"context.ApplyEvaluated(ref {contextVar});");
                ctx.AppendLine($"context.CommitChildContext(true, ref {contextVar});");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"context.PopChildContext(ref {contextVar});");
                ctx.PopIndent();
                ctx.AppendLine("}");

                if (needsLabel && i < entries.Count - 1)
                {
                    ctx.AppendLine();
                    ctx.AppendLine($"if (!context.RequiresEvaluationTracking && !context.HasCollector && {composedVar})");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"goto {endLabel};");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                }
            }

            if (needsLabel)
            {
                ctx.AppendLine();
                ctx.AppendLine($"{endLabel}:");
            }

            ctx.AppendLine($"context.EvaluatedKeyword({composedVar}, {composedVar} ? JsonSchemaEvaluation.MatchedAtLeastOneSchema : JsonSchemaEvaluation.DidNotMatchAtLeastOneSchema, {FormatUtf8Literal(keywordName)});");

            groupIdx++;
        }
    }

    private static void EmitOneOfValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        if (typeDeclaration.OneOfCompositionTypes() is not { } oneOf)
        {
            return;
        }

        bool useItems = typeDeclaration.RequiresItemsEvaluationTracking();
        bool useProps = typeDeclaration.RequiresPropertyEvaluationTracking();
        int groupIdx = 0;

        foreach (var kvp in oneOf)
        {
            string keywordName = kvp.Key.Keyword;
            List<SubschemaInfo> entries = ResolveSubschemaInfos(kvp.Value, subschemas);

            if (entries.Count == 0)
            {
                groupIdx++;
                continue;
            }

            string suffix = groupIdx > 0 ? groupIdx.ToString() : string.Empty;
            string countVar = $"oneOfMatchedCount{suffix}";
            string endLabel = $"oneOfEnd{suffix}";
            bool needsLabel = entries.Count > 1;

            // Try discriminator fast path
            if (CodeGenerationExtensions.TryGetOneOfDiscriminator(
                    kvp.Value,
                    out string? discriminatorPropertyName,
                    out List<(string Value, int BranchIndex)>? discriminatorValues,
                    out JsonValueKind discriminatorValueKind))
            {
                EmitOneOfDiscriminatorFastPath(
                    ctx, discriminatorPropertyName, discriminatorValues, discriminatorValueKind,
                    entries, useItems, useProps, suffix, keywordName);
            }

            // Sequential evaluation path (used when collector is present, or no discriminator)
            ctx.AppendLine();
            ctx.AppendLine($"int {countVar} = 0;");

            for (int i = 0; i < entries.Count; i++)
            {
                SubschemaInfo info = entries[i];
                string contextVar = $"oneOfCtx{suffix}_{i}";
                string pathField = info.PathFieldName ?? "null";
                string schemaPathField = info.SchemaPathFieldName ?? "null";

                ctx.AppendLine();
                ctx.AppendLine($"JsonSchemaContext {contextVar} =");
                ctx.PushIndent();
                ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(useItems || info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(useProps || info.UseEvaluatedProperties)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
                ctx.PopIndent();
                ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref {contextVar});");
                ctx.AppendLine($"if ({contextVar}.IsMatch)");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"{countVar}++;");
                ctx.AppendLine($"context.ApplyEvaluated(ref {contextVar});");
                ctx.AppendLine($"context.CommitChildContext(true, ref {contextVar});");
                ctx.PopIndent();
                ctx.AppendLine("}");
                ctx.AppendLine("else");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"context.PopChildContext(ref {contextVar});");
                ctx.PopIndent();
                ctx.AppendLine("}");

                if (needsLabel && i < entries.Count - 1)
                {
                    ctx.AppendLine();
                    ctx.AppendLine($"if (!context.RequiresEvaluationTracking && !context.HasCollector && {countVar} > 1)");
                    ctx.AppendLine("{");
                    ctx.PushIndent();
                    ctx.AppendLine($"goto {endLabel};");
                    ctx.PopIndent();
                    ctx.AppendLine("}");
                }
            }

            if (needsLabel)
            {
                ctx.AppendLine();
                ctx.AppendLine($"{endLabel}:");
            }

            string isMatchVar = $"oneOfIsMatch{suffix}";
            ctx.AppendLine($"bool {isMatchVar} = {countVar} == 1;");
            ctx.AppendLine($"context.EvaluatedKeyword({isMatchVar}, {countVar} == 0 ? JsonSchemaEvaluation.MatchedNoSchema : {countVar} == 1 ? JsonSchemaEvaluation.MatchedExactlyOneSchema : JsonSchemaEvaluation.MatchedMoreThanOneSchema, {FormatUtf8Literal(keywordName)});");

            groupIdx++;
        }
    }

    private static void EmitOneOfDiscriminatorFastPath(
        GenerationContext ctx,
        string discriminatorPropertyName,
        List<(string Value, int BranchIndex)> discriminatorValues,
        JsonValueKind discriminatorValueKind,
        List<SubschemaInfo> entries,
        bool useItems,
        bool useProps,
        string suffix,
        string keywordName)
    {
        string? mapFieldName = null;

        // Use hash map for 4+ string branches
        if (discriminatorValueKind == JsonValueKind.String && discriminatorValues.Count > MinEnumValuesForHashSet)
        {
            mapFieldName = $"OneOfDiscriminatorMap_{ctx.DeferredEnumStringMaps.Count}";
            string builderName = $"BuildOneOfDiscriminatorMap_{ctx.DeferredEnumStringMaps.Count}";
            string[] keys = discriminatorValues.Select(d => d.Value).ToArray();
            ctx.DeferredEnumStringMaps.Add((mapFieldName, builderName, keys));
        }

        string quotedPropertyName = SymbolDisplay.FormatLiteral(discriminatorPropertyName, true);

        ctx.AppendLine();
        ctx.AppendLine("if (!context.HasCollector)");
        ctx.AppendLine("{");
        ctx.PushIndent();

        // Find the discriminator property via direct lookup (uses property map if available, linear scan otherwise)
        ctx.AppendLine("int oneOfDiscriminatorBranch = -1;");
        ctx.AppendLine("if (parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.StartObject)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"if (parentDocument.TryGetNamedPropertyValue(parentIndex, {quotedPropertyName}u8, out IJsonDocument? oneOfDiscriminator_doc, out int oneOfDiscriminator_idx))");
        ctx.AppendLine("{");
        ctx.PushIndent();

        if (discriminatorValueKind == JsonValueKind.Number)
        {
            EmitNumericDiscriminatorValueMatch(ctx, discriminatorValues, "oneOfDiscriminatorBranch", "oneOfDiscriminator_doc", "oneOfDiscriminator_idx");
        }
        else
        {
            EmitStringDiscriminatorValueMatch(ctx, discriminatorValues, mapFieldName, "oneOfDiscriminatorBranch", "oneOfDiscriminator_doc", "oneOfDiscriminator_idx");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");  // close: if (TryGetNamedPropertyValue)
        ctx.PopIndent();
        ctx.AppendLine("}");  // close: if (StartObject)

        // Dispatch to matching branch via switch
        ctx.AppendLine();
        ctx.AppendLine("switch (oneOfDiscriminatorBranch)");
        ctx.AppendLine("{");

        int switchCaseIndex = 0;
        foreach ((_, int branchIndex) in discriminatorValues)
        {
            SubschemaInfo info = entries[branchIndex];
            string ctxVar = $"oneOfDiscCtx{suffix}_{branchIndex}";
            string pathField = info.PathFieldName ?? "null";
            string schemaPathField = info.SchemaPathFieldName ?? "null";

            ctx.PushIndent();
            ctx.AppendLine($"case {switchCaseIndex}:");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"JsonSchemaContext {ctxVar} =");
            ctx.PushIndent();
            ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(useItems || info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(useProps || info.UseEvaluatedProperties)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
            ctx.PopIndent();
            ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref {ctxVar});");
            ctx.AppendLine($"if ({ctxVar}.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"context.ApplyEvaluated(ref {ctxVar});");
            ctx.AppendLine($"context.CommitChildContext(true, ref {ctxVar});");
            ctx.AppendLine($"context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedExactlyOneSchema, {FormatUtf8Literal(keywordName)});");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine("else");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"context.CommitChildContext(false, ref {ctxVar});");
            ctx.AppendLine($"context.EvaluatedKeyword(false, JsonSchemaEvaluation.MatchedNoSchema, {FormatUtf8Literal(keywordName)});");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.PopIndent();
            switchCaseIndex++;
        }

        ctx.PushIndent();
        ctx.AppendLine("default:");
        ctx.PushIndent();
        ctx.AppendLine("break;");
        ctx.PopIndent();
        ctx.PopIndent();
        ctx.AppendLine("}");

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitStringDiscriminatorValueMatch(
        GenerationContext ctx,
        List<(string Value, int BranchIndex)> discriminatorValues,
        string? mapFieldName,
        string branchVar,
        string docVar,
        string idxVar)
    {
        ctx.AppendLine($"if ({docVar}.GetJsonTokenType({idxVar}) == JsonTokenType.String)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"using UnescapedUtf8JsonString discriminatorValue = {docVar}.GetUtf8JsonString({idxVar}, JsonTokenType.String);");

        if (mapFieldName is not null)
        {
            ctx.AppendLine($"{mapFieldName}.TryGetValue(discriminatorValue.Span, out {branchVar});");
        }
        else
        {
            int caseIndex = 0;
            foreach ((string value, _) in discriminatorValues)
            {
                string quotedValue = SymbolDisplay.FormatLiteral(value, true);
                string ifKeyword = caseIndex == 0 ? "if" : "else if";
                ctx.AppendLine($"{ifKeyword} (discriminatorValue.Span.SequenceEqual({quotedValue}u8))");
                ctx.AppendLine("{");
                ctx.PushIndent();
                ctx.AppendLine($"{branchVar} = {caseIndex};");
                ctx.PopIndent();
                ctx.AppendLine("}");
                caseIndex++;
            }
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitNumericDiscriminatorValueMatch(
        GenerationContext ctx,
        List<(string Value, int BranchIndex)> discriminatorValues,
        string branchVar,
        string docVar,
        string idxVar)
    {
        ctx.AppendLine($"if ({docVar}.GetJsonTokenType({idxVar}) == JsonTokenType.Number)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"ReadOnlyMemory<byte> discriminatorRawValue = {docVar}.GetRawSimpleValue({idxVar});");
        ctx.AppendLine("JsonElementHelpers.TryParseNumber(discriminatorRawValue.Span, out bool discriminatorIsNegative, out ReadOnlySpan<byte> discriminatorIntegral, out ReadOnlySpan<byte> discriminatorFractional, out int discriminatorExponent);");

        int caseIndex = 0;
        foreach ((string value, _) in discriminatorValues)
        {
            ReadOnlySpan<byte> rawValue = System.Text.Encoding.UTF8.GetBytes(value);
            Corvus.Text.Json.CodeGeneration.Internal.JsonElementHelpers.ParseNumber(rawValue, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);

            string isNegativeStr = isNegative ? "true" : "false";
            string integralStr = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(integral), true);
            string fractionalStr = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(fractional), true);
            string exponentStr = exponent.ToString();

            string ifKeyword = caseIndex == 0 ? "if" : "else if";
            ctx.AppendLine($"{ifKeyword} (JsonElementHelpers.CompareNormalizedJsonNumbers(discriminatorIsNegative, discriminatorIntegral, discriminatorFractional, discriminatorExponent, {isNegativeStr}, {integralStr}u8, {fractionalStr}u8, {exponentStr}) == 0)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"{branchVar} = {caseIndex};");
            ctx.PopIndent();
            ctx.AppendLine("}");
            caseIndex++;
        }

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitAnyOfDiscriminatorFastPath(
        GenerationContext ctx,
        string discriminatorPropertyName,
        List<(string Value, int BranchIndex)> discriminatorValues,
        JsonValueKind discriminatorValueKind,
        List<SubschemaInfo> entries,
        bool useItems,
        bool useProps,
        string suffix,
        string keywordName)
    {
        string? mapFieldName = null;

        // Use hash map for 4+ string branches
        if (discriminatorValueKind == JsonValueKind.String && discriminatorValues.Count > MinEnumValuesForHashSet)
        {
            mapFieldName = $"AnyOfDiscriminatorMap_{ctx.DeferredEnumStringMaps.Count}";
            string builderName = $"BuildAnyOfDiscriminatorMap_{ctx.DeferredEnumStringMaps.Count}";
            string[] keys = discriminatorValues.Select(d => d.Value).ToArray();
            ctx.DeferredEnumStringMaps.Add((mapFieldName, builderName, keys));
        }

        string quotedPropertyName = SymbolDisplay.FormatLiteral(discriminatorPropertyName, true);

        ctx.AppendLine();
        ctx.AppendLine("if (!context.HasCollector)");
        ctx.AppendLine("{");
        ctx.PushIndent();

        // Find the discriminator property via direct lookup (uses property map if available, linear scan otherwise)
        ctx.AppendLine("int anyOfDiscriminatorBranch = -1;");
        ctx.AppendLine("if (parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.StartObject)");
        ctx.AppendLine("{");
        ctx.PushIndent();
        ctx.AppendLine($"if (parentDocument.TryGetNamedPropertyValue(parentIndex, {quotedPropertyName}u8, out IJsonDocument? anyOfDiscriminator_doc, out int anyOfDiscriminator_idx))");
        ctx.AppendLine("{");
        ctx.PushIndent();

        if (discriminatorValueKind == JsonValueKind.Number)
        {
            EmitNumericDiscriminatorValueMatch(ctx, discriminatorValues, "anyOfDiscriminatorBranch", "anyOfDiscriminator_doc", "anyOfDiscriminator_idx");
        }
        else
        {
            EmitStringDiscriminatorValueMatch(ctx, discriminatorValues, mapFieldName, "anyOfDiscriminatorBranch", "anyOfDiscriminator_doc", "anyOfDiscriminator_idx");
        }

        ctx.PopIndent();
        ctx.AppendLine("}");  // close: if (TryGetNamedPropertyValue)
        ctx.PopIndent();
        ctx.AppendLine("}");  // close: if (StartObject)

        // Dispatch to matching branch via switch
        ctx.AppendLine();
        ctx.AppendLine("switch (anyOfDiscriminatorBranch)");
        ctx.AppendLine("{");

        int switchCaseIndex = 0;
        foreach ((_, int branchIndex) in discriminatorValues)
        {
            SubschemaInfo info = entries[branchIndex];
            string ctxVar = $"anyOfDiscCtx{suffix}_{branchIndex}";
            string pathField = info.PathFieldName ?? "null";
            string schemaPathField = info.SchemaPathFieldName ?? "null";

            ctx.PushIndent();
            ctx.AppendLine($"case {switchCaseIndex}:");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"JsonSchemaContext {ctxVar} =");
            ctx.PushIndent();
            ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(useItems || info.UseEvaluatedItems)}, useEvaluatedProperties: {BoolLiteral(useProps || info.UseEvaluatedProperties)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
            ctx.PopIndent();
            ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref {ctxVar});");
            ctx.AppendLine($"if ({ctxVar}.IsMatch)");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"context.ApplyEvaluated(ref {ctxVar});");
            ctx.AppendLine($"context.CommitChildContext(true, ref {ctxVar});");
            ctx.AppendLine($"context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedAtLeastOneSchema, {FormatUtf8Literal(keywordName)});");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine("else");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine($"context.CommitChildContext(false, ref {ctxVar});");
            ctx.AppendLine($"context.EvaluatedKeyword(false, JsonSchemaEvaluation.DidNotMatchAtLeastOneSchema, {FormatUtf8Literal(keywordName)});");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine();
            ctx.AppendLine("return;");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.PopIndent();
            switchCaseIndex++;
        }

        // Default: discriminator value not recognized or property not found — fall through to sequential
        ctx.PushIndent();
        ctx.AppendLine("default:");
        ctx.PushIndent();
        ctx.AppendLine("break;");
        ctx.PopIndent();
        ctx.PopIndent();
        ctx.AppendLine("}");

        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static void EmitNotValidation(
        GenerationContext ctx,
        TypeDeclaration typeDeclaration,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        foreach (INotValidationKeyword notKeyword in typeDeclaration.Keywords().OfType<INotValidationKeyword>())
        {
            if (!notKeyword.TryGetNotType(typeDeclaration, out ReducedTypeDeclaration? notType) ||
                notType is not ReducedTypeDeclaration notDecl)
            {
                continue;
            }

            string subLoc = notDecl.ReducedType.LocatedSchema.Location.ToString();
            if (!subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
            {
                continue;
            }

            string keywordName = notKeyword.Keyword;
            string pathField = info.PathFieldName ?? "null";
            string schemaPathField = info.SchemaPathFieldName ?? "null";

            bool notUseItems = notDecl.ReducedType.RequiresItemsEvaluationTracking();
            bool notUseProps = notDecl.ReducedType.RequiresPropertyEvaluationTracking();

            ctx.AppendLine();
            ctx.AppendLine("JsonSchemaContext notContext =");
            ctx.PushIndent();
            ctx.AppendLine($"context.PushChildContext(parentDocument, parentIndex, useEvaluatedItems: {BoolLiteral(notUseItems)}, useEvaluatedProperties: {BoolLiteral(notUseProps)}, evaluationPath: {pathField}, schemaEvaluationPath: {schemaPathField});");
            ctx.PopIndent();
            ctx.AppendLine($"{info.MethodName}(parentDocument, parentIndex, ref notContext);");
            ctx.AppendLine("bool notIsMatch = !notContext.IsMatch;");
            ctx.AppendLine("context.PopChildContext(ref notContext);");
            ctx.AppendLine($"context.EvaluatedKeyword(notIsMatch, notIsMatch ? JsonSchemaEvaluation.MatchedNotSchema : JsonSchemaEvaluation.DidNotMatchNotSchema, {FormatUtf8Literal(keywordName)});");
        }
    }

    private static List<SubschemaInfo> ResolveSubschemaInfos(
        IReadOnlyCollection<TypeDeclaration> subTypes,
        Dictionary<string, SubschemaInfo> subschemas)
    {
        var result = new List<SubschemaInfo>();
        foreach (TypeDeclaration subType in subTypes)
        {
            string subLoc = subType.LocatedSchema.Location.ToString();
            if (subschemas.TryGetValue(subLoc, out SubschemaInfo? info))
            {
                result.Add(info);
            }
        }

        return result;
    }

    private static bool TryGetSingleCoreType(CoreTypes types, out CoreTypes singleType)
    {
        singleType = CoreTypes.None;
        int count = 0;

        foreach (CoreTypes candidate in new[]
        {
            CoreTypes.Object,
            CoreTypes.Array,
            CoreTypes.String,
            CoreTypes.Number,
            CoreTypes.Integer,
            CoreTypes.Boolean,
            CoreTypes.Null,
        })
        {
            if ((types & candidate) != 0)
            {
                singleType = candidate;
                count++;
                if (count > 1)
                {
                    return false;
                }
            }
        }

        return count == 1;
    }

    private static string GetMatchMethodName(CoreTypes type)
    {
        return type switch
        {
            CoreTypes.Object => "MatchTypeObject",
            CoreTypes.Array => "MatchTypeArray",
            CoreTypes.String => "MatchTypeString",
            CoreTypes.Number => "MatchTypeNumber",
            CoreTypes.Integer => "MatchTypeInteger",
            CoreTypes.Boolean => "MatchTypeBoolean",
            CoreTypes.Null => "MatchTypeNull",
            _ => throw new System.InvalidOperationException($"Unexpected core type: {type}"),
        };
    }

    private static string GetIgnoredNotTypeName(CoreTypes type)
    {
        return type switch
        {
            CoreTypes.Object => "IgnoredNotTypeObject",
            CoreTypes.Array => "IgnoredNotTypeArray",
            CoreTypes.String => "IgnoredNotTypeString",
            CoreTypes.Number => "IgnoredNotTypeNumber",
            CoreTypes.Integer => "IgnoredNotTypeInteger",
            CoreTypes.Boolean => "IgnoredNotTypeBoolean",
            CoreTypes.Null => "IgnoredNotTypeNull",
            _ => throw new System.InvalidOperationException($"Unexpected core type: {type}"),
        };
    }

    private static string GetTypeKeywordLiteral(TypeDeclaration typeDeclaration)
    {
        foreach (IKeyword kw in typeDeclaration.Keywords())
        {
            if (kw is ICoreTypeValidationKeyword)
            {
                return FormatUtf8Literal(kw.Keyword);
            }
        }

        return FormatUtf8Literal("type");
    }

    private static List<string> GetTypeSensitiveKeywordNames(TypeDeclaration typeDeclaration, CoreTypes targetType)
    {
        var names = new List<string>();

        foreach (IKeyword keyword in typeDeclaration.Keywords())
        {
            bool isSensitive = targetType switch
            {
                CoreTypes.Object => keyword is IObjectValidationKeyword,
                CoreTypes.Array => keyword is IArrayValidationKeyword,
                CoreTypes.String => keyword is IStringValidationKeyword,
                CoreTypes.Number or CoreTypes.Integer => keyword is INumberValidationKeyword,
                CoreTypes.Boolean => keyword is IBooleanValidationKeyword,
                CoreTypes.Null => keyword is INullValidationKeyword,
                _ => false,
            };

            if (isSensitive)
            {
                names.Add(keyword.Keyword);
            }
        }

        return names;
    }

    private static string GetEvaluatorClassName(TypeDeclaration rootType)
    {
        if (rootType.TryGetDotnetTypeName(out string? typeName))
        {
            return typeName + "Evaluator";
        }

        // The unreduced root may not have a user-specified name (it's set on the reduced type).
        // Fall back to the reduced type's name if available.
        TypeDeclaration reduced = rootType.ReducedTypeDeclaration().ReducedType;
        if (reduced != rootType && reduced.TryGetDotnetTypeName(out typeName))
        {
            return typeName + "Evaluator";
        }

        string location = rootType.LocatedSchema.Location.ToString();
        string safeName = MakeSafeIdentifier(location);
        return safeName + "Evaluator";
    }

    private static string MakeSafeIdentifier(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
            {
                sb.Append('_');
            }
        }

        if (sb.Length == 0 || !char.IsLetter(sb[0]))
        {
            sb.Insert(0, "Schema");
        }

        return sb.ToString().TrimEnd('_');
    }

    private static string BuildPatternPropertyCondition(PatternPropertyDeclaration patternProp, RegexPatternCategory category)
    {
        return category switch
        {
            RegexPatternCategory.NonEmpty => "unescapedPropertyName.Span.Length > 0",
            RegexPatternCategory.Prefix => $"unescapedPropertyName.Span.StartsWith({SymbolDisplay.FormatLiteral(CodeGenerationExtensions.ExtractRegexPrefix(patternProp.Pattern), true)}u8)",
            RegexPatternCategory.Range => $"JsonSchemaEvaluation.MatchRangeRegularExpression(unescapedPropertyName.Span, {CodeGenerationExtensions.ExtractRegexRange(patternProp.Pattern).Min}, {CodeGenerationExtensions.ExtractRegexRange(patternProp.Pattern).Max})",
            _ => $"JsonSchemaEvaluation.MatchRegularExpression(unescapedPropertyName.Span, PatternRegex_{MakeSafeIdentifier(patternProp.Pattern)})",
        };
    }

    private static bool AnyNamedPropertyMatchesPatternProperty(
        List<(PropertyDeclaration Property, SubschemaInfo Info)> properties,
        List<(PatternPropertyDeclaration PatternProp, SubschemaInfo Info)> patternProperties)
    {
        if (patternProperties.Count == 0)
        {
            return false;
        }

        IEnumerable<string> propertyNames = properties.Select(p => p.Property.JsonPropertyName);

        foreach ((PatternPropertyDeclaration patternProp, _) in patternProperties)
        {
            RegexPatternCategory category = CodeGenerationExtensions.ClassifyRegexPattern(patternProp.Pattern);

            switch (category)
            {
                case RegexPatternCategory.Noop:
                case RegexPatternCategory.NonEmpty:
                    // .* or .+ matches everything — overlap is guaranteed
                    return true;

                case RegexPatternCategory.Prefix:
                {
                    string prefix = CodeGenerationExtensions.ExtractRegexPrefix(patternProp.Pattern);
                    if (propertyNames.Any(name => name.StartsWith(prefix, StringComparison.Ordinal)))
                    {
                        return true;
                    }

                    break;
                }

                case RegexPatternCategory.Range:
                {
                    (int min, int max) = CodeGenerationExtensions.ExtractRegexRange(patternProp.Pattern);
                    if (propertyNames.Any(name => name.Length >= min && name.Length <= max))
                    {
                        return true;
                    }

                    break;
                }

                default:
                {
                    try
                    {
                        Regex regex = new(patternProp.Pattern);
                        if (propertyNames.Any(name => regex.IsMatch(name)))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // If the regex is invalid, assume overlap for safety
                        return true;
                    }

                    break;
                }
            }
        }

        return false;
    }

    private static void DeduplicatePropertyIdentifiers(List<PropertyMatcherEntry> entries)
    {
        var seen = new Dictionary<string, int>();
        foreach (PropertyMatcherEntry entry in entries)
        {
            string id = entry.SafeIdentifier;
            if (seen.TryGetValue(id, out int count))
            {
                seen[id] = count + 1;
                entry.SafeIdentifier = $"{id}_{count + 1}";
            }
            else
            {
                seen[id] = 1;
            }
        }
    }

    private static string FormatUtf8Literal(string value)
    {
        return SymbolDisplay.FormatLiteral(value, true) + "u8";
    }

    private static string BoolLiteral(bool value)
    {
        return value ? "true" : "false";
    }

    private const int MinEnumValuesForHashSet = 3;

    private static bool TryGetAllInt64ValuesFromElements(JsonElement[] elements, [NotNullWhen(true)] out long[]? longValues)
    {
        longValues = new long[elements.Length];

        for (int i = 0; i < elements.Length; i++)
        {
            if (!elements[i].TryGetInt64(out longValues[i]))
            {
                longValues = null;
                return false;
            }
        }

        return true;
    }

    private static void EmitFileHeader(GenerationContext ctx, CSharpLanguageProvider.Options options)
    {
        ctx.AppendLine("// <auto-generated/>");
        ctx.AppendLine("#nullable enable");
        ctx.AppendLine();

        if (options.AddExplicitUsings)
        {
            ctx.AppendLine("using System;");
        }

        ctx.AppendLine("using System.Diagnostics;");
        ctx.AppendLine("using System.Diagnostics.CodeAnalysis;");
        ctx.AppendLine("using System.Buffers;");
        ctx.AppendLine("using System.Buffers.Text;");
        ctx.AppendLine("using System.Runtime.CompilerServices;");
        ctx.AppendLine("using Corvus.Text.Json;");
        ctx.AppendLine("using Corvus.Text.Json.Internal;");
        ctx.AppendLine();
    }

    private static void EmitNamespaceOpen(GenerationContext ctx, string ns)
    {
        ctx.AppendLine($"namespace {ns};");
        ctx.AppendLine();
    }

    private static void EmitClassOpen(GenerationContext ctx, string className)
    {
        ctx.AppendLine("/// <summary>");
        ctx.AppendLine($"/// Standalone schema evaluator for the <c>{className.Replace("Evaluator", string.Empty)}</c> schema.");
        ctx.AppendLine("/// </summary>");
        ctx.AppendLine($"public static class {className}");
        ctx.AppendLine("{");
        ctx.PushIndent();
    }

    private static void EmitClassClose(GenerationContext ctx)
    {
        ctx.PopIndent();
        ctx.AppendLine("}");
    }

    private static string GetOrCreateConstantField(GenerationContext ctx, string rawJsonText)
    {
        if (ctx.DeferredConstantFields.TryGetValue(rawJsonText, out string? existingName))
        {
            return existingName;
        }

        string fieldName = $"ConstDoc_{ctx.DeferredConstantFields.Count}";
        ctx.DeferredConstantFields[rawJsonText] = fieldName;
        return fieldName;
    }

    private static void EmitDeferredConstantFields(GenerationContext ctx)
    {
        if (ctx.DeferredConstantFields.Count == 0 && ctx.DeferredEnumStringSets.Count == 0 && ctx.DeferredEnumStringMaps.Count == 0)
        {
            return;
        }

        ctx.AppendLine();
        foreach (KeyValuePair<string, string> kvp in ctx.DeferredConstantFields)
        {
            string quotedRawText = SymbolDisplay.FormatLiteral(kvp.Key, true);
            ctx.AppendLine($"private static readonly ParsedJsonDocument<JsonElement> {kvp.Value} = ParsedJsonDocument<JsonElement>.Parse({quotedRawText});");
        }

        foreach ((string fieldName, string builderName, string[] values) in ctx.DeferredEnumStringSets)
        {
            ctx.AppendLine();
            ctx.AppendLine($"private static EnumStringSet {builderName}()");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return new EnumStringSet([");
            ctx.PushIndent();
            foreach (string val in values)
            {
                string quotedVal = SymbolDisplay.FormatLiteral(val, true);
                ctx.AppendLine($"static () => {quotedVal}u8,");
            }

            ctx.PopIndent();
            ctx.AppendLine("]);");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine();
            ctx.AppendLine($"private static EnumStringSet {fieldName} {{ get; }} = {builderName}();");
        }

        foreach ((string fieldName, string builderName, string[] keys) in ctx.DeferredEnumStringMaps)
        {
            ctx.AppendLine();
            ctx.AppendLine($"private static EnumStringMap {builderName}()");
            ctx.AppendLine("{");
            ctx.PushIndent();
            ctx.AppendLine("return new EnumStringMap([");
            ctx.PushIndent();
            foreach (string key in keys)
            {
                string quotedKey = SymbolDisplay.FormatLiteral(key, true);
                ctx.AppendLine($"static () => {quotedKey}u8,");
            }

            ctx.PopIndent();
            ctx.AppendLine("]);");
            ctx.PopIndent();
            ctx.AppendLine("}");
            ctx.AppendLine();
            ctx.AppendLine($"private static EnumStringMap {fieldName} {{ get; }} = {builderName}();");
        }
    }

    /// <summary>
    /// Tracks property matcher information for a schema that has named properties.
    /// </summary>
    internal sealed class PropertyMatcherInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMatcherInfo"/> class.
        /// </summary>
        /// <param name="methodName">The evaluation method name for the schema.</param>
        public PropertyMatcherInfo(string methodName)
        {
            MethodName = methodName;
        }

        /// <summary>
        /// Gets the evaluation method name (e.g. "EvaluateRoot").
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Gets the list of named property entries.
        /// </summary>
        public List<PropertyMatcherEntry> Properties { get; } = [];

        /// <summary>
        /// Gets a value indicating whether to use a hash-based <see cref="PropertySchemaMatchers{T}"/>
        /// map (true for &gt;1 properties) or a simple if/SequenceEqual chain.
        /// </summary>
        public bool UseMap => Properties.Count > 1;

        /// <summary>
        /// Gets the delegate type name for this schema's property validators.
        /// </summary>
        public string DelegateTypeName => $"NamedPropertyValidator_{MethodName}";

        /// <summary>
        /// Gets the TryGetNamedMatcher method name for this schema.
        /// </summary>
        public string TryGetMethodName => $"TryGetNamedMatcher_{MethodName}";
    }

    /// <summary>
    /// Tracks information about a single named property in a property matcher.
    /// </summary>
    internal sealed class PropertyMatcherEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMatcherEntry"/> class.
        /// </summary>
        /// <param name="jsonPropertyName">The JSON property name.</param>
        /// <param name="info">The subschema info for the property type.</param>
        /// <param name="requiredIndex">The index in the required property list, or -1 if not required.</param>
        public PropertyMatcherEntry(string jsonPropertyName, SubschemaInfo info, int requiredIndex)
        {
            JsonPropertyName = jsonPropertyName;
            Info = info;
            RequiredIndex = requiredIndex;
            SafeIdentifier = MakeSafeIdentifier(JsonPropertyName);
        }

        /// <summary>
        /// Gets the JSON property name (e.g. "name").
        /// </summary>
        public string JsonPropertyName { get; }

        /// <summary>
        /// Gets the subschema info for the property type.
        /// </summary>
        public SubschemaInfo Info { get; }

        /// <summary>
        /// Gets the index in the required property list, or -1 if not required.
        /// </summary>
        public int RequiredIndex { get; }

        /// <summary>
        /// Gets the safe identifier name derived from the property name.
        /// </summary>
        public string SafeIdentifier { get; internal set; }

        /// <summary>
        /// Gets the UTF-8 constant field name.
        /// </summary>
        public string Utf8FieldName(string methodName) => $"PropNameUtf8_{methodName}_{SafeIdentifier}";

        /// <summary>
        /// Gets the per-property matcher method name.
        /// </summary>
        public string MatchMethodName(string methodName) => $"MatchProperty_{methodName}_{SafeIdentifier}";
    }

    /// <summary>
    /// Tracks information about a subschema for code generation.
    /// </summary>
    internal sealed class SubschemaInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubschemaInfo"/> class.
        /// </summary>
        /// <param name="methodName">The evaluation method name.</param>
        /// <param name="schemaPath">The schema evaluation path.</param>
        /// <param name="typeDeclaration">The type declaration for this subschema.</param>
        /// <param name="useEvaluatedItems">Whether items evaluation tracking is needed.</param>
        /// <param name="useEvaluatedProperties">Whether property evaluation tracking is needed.</param>
        public SubschemaInfo(
            string methodName,
            string schemaPath,
            TypeDeclaration typeDeclaration,
            bool useEvaluatedItems,
            bool useEvaluatedProperties)
        {
            this.MethodName = methodName;
            this.SchemaPath = schemaPath;
            this.TypeDeclaration = typeDeclaration;
            this.UseEvaluatedItems = useEvaluatedItems;
            this.UseEvaluatedProperties = useEvaluatedProperties;
        }

        /// <summary>
        /// Gets the evaluation method name (e.g. "EvaluateAllOf0").
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Gets the schema evaluation path (e.g. "#/allOf/0").
        /// </summary>
        public string SchemaPath { get; }

        /// <summary>
        /// Gets the type declaration for this subschema.
        /// </summary>
        public TypeDeclaration TypeDeclaration { get; }

        /// <summary>
        /// Gets a value indicating whether items evaluation tracking is needed.
        /// </summary>
        public bool UseEvaluatedItems { get; }

        /// <summary>
        /// Gets a value indicating whether property evaluation tracking is needed.
        /// </summary>
        public bool UseEvaluatedProperties { get; }

        /// <summary>
        /// Gets or sets the path provider field name (set during field emission).
        /// </summary>
        public string? PathFieldName { get; set; }

        /// <summary>
        /// Gets or sets the schema evaluation path provider field name (set during field emission).
        /// </summary>
        public string? SchemaPathFieldName { get; set; }
    }

    /// <summary>
    /// Lightweight code builder with indentation support.
    /// </summary>
    internal sealed class GenerationContext
    {
        private readonly StringBuilder builder = new();
        private readonly string lineEnd;
        private int indentLevel;
        private string currentIndent = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerationContext"/> class.
        /// </summary>
        /// <param name="lineEnd">The line ending sequence.</param>
        public GenerationContext(string lineEnd)
        {
            this.lineEnd = lineEnd;
        }

        /// <summary>
        /// Gets the deferred constant document fields. Maps raw JSON text to field name.
        /// </summary>
        public Dictionary<string, string> DeferredConstantFields { get; } = [];

        /// <summary>
        /// Gets the deferred EnumStringSet fields. Maps a unique key to (fieldName, builderName, list of string values).
        /// </summary>
        public List<(string FieldName, string BuilderName, string[] Values)> DeferredEnumStringSets { get; } = [];

        /// <summary>
        /// Gets the deferred EnumStringMap fields for oneOf discriminator dispatch.
        /// </summary>
        public List<(string FieldName, string BuilderName, string[] Keys)> DeferredEnumStringMaps { get; } = [];

        /// <summary>
        /// Increases the indentation level by one.
        /// </summary>
        public void PushIndent()
        {
            this.indentLevel++;
            this.currentIndent = new string(' ', this.indentLevel * 4);
        }

        /// <summary>
        /// Decreases the indentation level by one.
        /// </summary>
        public void PopIndent()
        {
            this.indentLevel--;
            this.currentIndent = this.indentLevel > 0
                ? new string(' ', this.indentLevel * 4)
                : string.Empty;
        }

        /// <summary>
        /// Appends an empty line (line ending only, no indent).
        /// </summary>
        public void AppendLine()
        {
            this.builder.Append(this.lineEnd);
        }

        /// <summary>
        /// Appends a line at the current indent level, followed by a line ending.
        /// </summary>
        /// <param name="line">The text content of the line.</param>
        public void AppendLine(string line)
        {
            this.builder.Append(this.currentIndent);
            this.builder.Append(line);
            this.builder.Append(this.lineEnd);
        }

        /// <summary>
        /// Appends a line at the current indent level, followed by a line ending.
        /// Equivalent to <see cref="AppendLine(string)"/>; provides a self-documenting
        /// call site when the string is already logically indented content.
        /// </summary>
        /// <param name="line">The text content of the line.</param>
        public void AppendLineIndent(string line)
        {
            this.AppendLine(line);
        }

        /// <summary>
        /// Appends raw text inline (no indent, no line ending).
        /// </summary>
        /// <param name="text">The raw text to append.</param>
        public void AppendRaw(string text)
        {
            this.builder.Append(text);
        }

        /// <summary>
        /// Appends text without a line ending (no indent prefix).
        /// </summary>
        /// <param name="text">The text to append.</param>
        public void Append(string text)
        {
            this.builder.Append(text);
        }

        /// <summary>
        /// Gets the current length of the generated output.
        /// </summary>
        public int Length => this.builder.Length;

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.builder.ToString();
        }
    }
}