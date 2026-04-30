// <copyright file="PropertiesValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// A properties validation handler.
/// </summary>
public class PropertiesValidationHandler : IChildObjectPropertyValidationHandler2, IJsonSchemaClassSetup
{
    private const string PropertyValidatorDelegateNameKey = "PropertiesValidationHandler.PropertyValidatorDelegateName";

    internal const int MinPropertiesForMap = 1;

    internal List<INamedPropertyChildHandler> children = [];

    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertiesValidationHandler"/>.
    /// </summary>
    public static PropertiesValidationHandler Instance { get; } = CreateDefaultInstance();

    private static PropertiesValidationHandler CreateDefaultInstance()
    {
        PropertiesValidationHandler instance = new();
        instance.children.AddRange(
            [
                PropertySubschemaChildHandler.Instance,
                DependentSchemasChildHandler.Instance,
                RequiredPropertyChildHandler.Instance
            ]);

        return instance;
    }

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.AfterComposition + 1;

    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        HashSet<PropertyDeclaration> propertiesToGenerate = [];

        foreach (INamedPropertyChildHandler child in children)
        {
            child.BeginJsonSchemaClassSetup(generator, typeDeclaration);
        }

        foreach (PropertyDeclaration property in typeDeclaration.PropertyDeclarations)
        {
            foreach (INamedPropertyChildHandler child in children)
            {
                if (child.AppendJsonSchemaClassSetupForProperty(generator, typeDeclaration, property))
                {
                    propertiesToGenerate.Add(property);
                }
            }
        }

        // Check for hoisted allOf branches that might need a unified map
        bool hasHoistedBranches = typeDeclaration.TryGetMetadata(
            HoistedAllOfPropertyValidationHandler.HoistedBranchMetadataKey,
            out List<HoistedAllOfPropertyValidationHandler.HoistedBranchMetadata>? hoistedBranches) &&
            hoistedBranches is not null;

        int totalHoistedProperties = hasHoistedBranches
            ? hoistedBranches!.Sum(b => b.Properties.Count)
            : 0;

        bool shouldBuildUnifiedMap = hasHoistedBranches &&
            totalHoistedProperties >= HoistedAllOfPropertyValidationHandler.MinHoistedPropertiesForMap;

        if (propertiesToGenerate.Count == 0 && !shouldBuildUnifiedMap)
        {
            return generator;
        }

        List<(string, string)> propertyAndMethodNames = [];

        string propertyValidatorDelegateName = generator.GetOrEmitNamedTypeInRootNamespace("PropertiesValidationHandler_NamedPropertyValidator", BuildValidatorDelegateKey(typeDeclaration, children), (generator, globalName) =>
        {
            generator
                .AppendIndent("internal delegate void ", globalName, "(IJsonDocument parentDocument, int parentDocumentIndex, int propertyCount, ref JsonSchemaContext context")
                .AppendNamedPropertyValidatorParameters(typeDeclaration, children)
                .AppendLine(");");
        });

        foreach (PropertyDeclaration property in propertiesToGenerate)
        {
            string propertyName = property.DotnetPropertyName();

            string methodName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("Match", suffix: propertyName);

            propertyAndMethodNames.Add((propertyName, methodName));

            bool requiresAddLocalEvaluatedProperty = children.Any(c => c.WillEmitCodeFor(typeDeclaration) && c.EvaluatesProperty(property));

            generator
                .AppendSeparatorLine()
                .AppendIndent("private static void ", methodName, "(IJsonDocument parentDocument, int parentDocumentIndex, int propertyCount, ref JsonSchemaContext context")
                .AppendNamedPropertyValidatorParameters(typeDeclaration, children)
                .AppendLine(")")
                .AppendLineIndent("{")
                .PushIndent()
                    .ConditionallyAppend(requiresAddLocalEvaluatedProperty, g => g.AppendLineIndent("context.AddLocalEvaluatedProperty(propertyCount);"))
                    .AppendChildObjectPropertyValidationCode(typeDeclaration, children, property)
                .PopIndent()
                .AppendLineIndent("}");
        }

        if (shouldBuildUnifiedMap)
        {
            // Build a unified map containing both local and hoisted property names
            List<HoistedAllOfPropertyValidationHandler.UnifiedMapLocalEntry> localEntries = [];
            int index = 0;
            foreach ((string propertyName, string methodName) in propertyAndMethodNames)
            {
                localEntries.Add(new HoistedAllOfPropertyValidationHandler.UnifiedMapLocalEntry(index, methodName, propertyName));
                index++;
            }

            HoistedAllOfPropertyValidationHandler.EmitPropertyIndexMap(generator, hoistedBranches!, localEntries);

            var unifiedMap = new HoistedAllOfPropertyValidationHandler.UnifiedMapInfo(localEntries);
            typeDeclaration.SetMetadata(HoistedAllOfPropertyValidationHandler.UnifiedMapMetadataKey, unifiedMap);
        }
        else
        {
            generator
                .BuildPropertyValidatorMap(typeDeclaration, children, propertyAndMethodNames, propertyValidatorDelegateName);

            typeDeclaration.SetMetadata(PropertyValidatorDelegateNameKey, propertyValidatorDelegateName);
        }

        foreach (INamedPropertyChildHandler child in children)
        {
            child.EndJsonSchemaClassSetup(generator, typeDeclaration);
        }

        return generator;
    }

    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { return generator; }

    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (INamedPropertyChildHandler child in children)
        {
            child.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }

    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (INamedPropertyChildHandler child in children
                    .OrderBy(c => c.ValidationHandlerPriority))
        {
            child.AppendValidationCode(generator, typeDeclaration);
        }

        return generator;
    }

    public CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If a unified map was built, the HoistedAllOfPropertyValidationHandler emits the
        // unified switch covering both local and hoisted properties. Skip our own lookup.
        if (typeDeclaration.TryGetMetadata(HoistedAllOfPropertyValidationHandler.UnifiedMapMetadataKey,
            out HoistedAllOfPropertyValidationHandler.UnifiedMapInfo? _))
        {
            return generator;
        }

        if (!typeDeclaration.TryGetMetadata(PropertyValidatorDelegateNameKey, out string? propertyValidatorDelegateName) ||
            propertyValidatorDelegateName is null)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (TryGetNamedMatcher(objectValidation_unescapedPropertyName.Span, out ", propertyValidatorDelegateName, "? validator))")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendValidatorCall(typeDeclaration, children)
            .AppendNoCollectorNoMatchShortcutReturn()
            .PopIndent()
            .AppendLineIndent("}");
    }

    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => children.Any(c => c.WillEmitCodeFor(typeDeclaration));

    private static string BuildValidatorDelegateKey(TypeDeclaration typeDeclaration, List<INamedPropertyChildHandler> children)
    {
        StringBuilder builder = new("_");
        foreach (INamedPropertyChildHandler child in children)
        {
            foreach (ObjectPropertyValidatorParameter parameter in child.GetNamedPropertyValidatorParameters(typeDeclaration))
            {
                builder.Append(parameter.DotnetTypeName);
                builder.Append('_');
                builder.Append(parameter.Name);
                builder.Append('_');
            }
        }

        return builder.ToString();
    }

    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => children.Any(c => c.WillEmitCodeFor(typeDeclaration));

    /// <summary>
    /// Emits a direct call to a local property's static validation method (used by the unified switch).
    /// </summary>
    internal static void AppendLocalPropertyDirectCall(CodeGenerator generator, TypeDeclaration typeDeclaration, string methodName)
    {
        generator
            .AppendIndent(methodName, "(parentDocument, objectValidation_currentIndex, objectValidation_propertyCount, ref context");

        foreach (INamedPropertyChildHandler child in Instance.children)
        {
            child.AppendValidatorArguments(generator, typeDeclaration);
        }

        generator
            .AppendLine(");");
    }
}

file static class PropertiesValidationHandlerExtensions
{
    public static CodeGenerator AppendValidatorCall(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<INamedPropertyChildHandler> children)
    {
        generator
            .AppendIndent("validator!(parentDocument, objectValidation_currentIndex, objectValidation_propertyCount, ref context");

        foreach (INamedPropertyChildHandler child in children)
        {
            child.AppendValidatorArguments(generator, typeDeclaration);
        }

        return generator
            .AppendLine(");");
    }

    public static CodeGenerator AppendNamedPropertyValidatorParameters(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<INamedPropertyChildHandler> children)
    {
        foreach (INamedPropertyChildHandler child in children)
        {
            foreach (ObjectPropertyValidatorParameter parameter in child.GetNamedPropertyValidatorParameters(typeDeclaration))
            {
                generator
                    .Append(", ")
                    .Append(parameter.DotnetTypeName)
                    .Append(" ")
                    .Append(parameter.Name);
            }
        }

        return generator;
    }

    public static CodeGenerator AppendChildObjectPropertyValidationCode(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<INamedPropertyChildHandler> children, PropertyDeclaration propertyDeclaration)
    {
        bool needsShortCut = false;

        foreach (INamedPropertyChildHandler child in children)
        {
            int length = generator.Length;

            if (needsShortCut)
            {
                generator
                    .AppendNoCollectorNoMatchShortcutReturn();
            }

            int currentLength = generator.Length;

            child.AppendObjectPropertyValidationCode(generator, typeDeclaration, propertyDeclaration);

            if (generator.Length == currentLength)
            {
                // Trim off the shortcut we speculatively appended
                generator.Length = length;
            }
            else
            {
                needsShortCut = true;
            }
        }

        return generator;
    }

    public static CodeGenerator BuildPropertyValidatorMap(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<INamedPropertyChildHandler> children, List<(string propertyName, string methodName)> propertyAndMethodNames, string propertyValidatorDelegateName)
    {
        string jsonPropertyNamesClassName = generator.JsonPropertyNamesClassName();

        if (propertyAndMethodNames.Count > PropertiesValidationHandler.MinPropertiesForMap)
        {
            string mapName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("Matchers");
            string builderName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("MatchersBuilder");

            // We are building the map.
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("private static PropertySchemaMatchers<", propertyValidatorDelegateName, "> ", builderName, "()")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new PropertySchemaMatchers<", propertyValidatorDelegateName, ">([")
                    .PushIndent();

            foreach ((string propertyName, string methodName) in propertyAndMethodNames)
            {
                generator
                        .AppendLineIndent("(static () => ", jsonPropertyNamesClassName, ".", propertyName, "Utf8, ", methodName, "),");
            }

            generator
                    .PopIndent()
                    .AppendLineIndent("]);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("private static PropertySchemaMatchers<", propertyValidatorDelegateName, "> ", mapName, " { get; } = ", builderName, "();")
                .AppendSeparatorLine()
                .ReserveName("TryGetNamedMatcher")
                .AppendLineIndent("private static bool TryGetNamedMatcher(ReadOnlySpan<byte> span,")
                .AppendLine("#if NET")
                .AppendLineIndent("[NotNullWhen(true)]")
                .AppendLine("#endif")
                .AppendLineIndent("out ", propertyValidatorDelegateName, "? matcher)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return ", mapName, ".TryGetNamedMatcher(span, out matcher);")
                .PopIndent()
                .AppendLineIndent("}");
        }
        else if (propertyAndMethodNames.Count > 0)
        {
            generator
                .AppendSeparatorLine()
                .ReserveName("TryGetNamedMatcher")
                .AppendLineIndent("private static bool TryGetNamedMatcher(ReadOnlySpan<byte> span,")
                .AppendLine("#if NET")
                .AppendLineIndent("[NotNullWhen(true)]")
                .AppendLine("#endif")
                .AppendLineIndent("out ", propertyValidatorDelegateName, "? matcher)")
                .AppendLineIndent("{")
                .PushIndent();

            foreach ((string propertyName, string methodName) in propertyAndMethodNames)
            {
                generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (span.SequenceEqual(", jsonPropertyNamesClassName, ".", propertyName, "Utf8))")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("matcher = ", methodName, ";")
                            .AppendLineIndent("return true;")
                        .PopIndent()
                        .AppendLineIndent("}");
            }

            generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("matcher = default;")
                    .AppendLineIndent("return false;")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }
}