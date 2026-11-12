// <copyright file="DependentSchemasValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A property name validation handler.
/// </summary>
public class DependentSchemasValidationHandler : IChildObjectPropertyValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="DependentSchemasValidationHandler"/>.
    /// </summary>
    public static DependentSchemasValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The dependent schemas are applied before the properties are enumerated, as the in-place applicators are. A
    /// dependent schema can evaluate any property of the object, and the enumeration checks each property against
    /// <c>unevaluatedProperties</c> as it reaches it, so a dependent schema applied when the enumeration reached its
    /// property would come too late for the properties before it.
    /// </remarks>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.DependentSchemasSubschemaTypes()
            is IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> dependentSchemas)
        {
            foreach (IObjectPropertyDependentSchemasValidationKeyword keyword in dependentSchemas.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                int i = 0;

                foreach (DependentSchemaDeclaration dependentSchema in dependentSchemas[keyword])
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    string resultName = generator.GetUniqueVariableNameInScope("Result", prefix: keyword.Keyword, suffix: i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    string quotedPropertyName = SymbolDisplay.FormatLiteral(dependentSchema.JsonPropertyName, true);
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            "if (value.HasJsonElementBacking ? value.HasProperty(",
                            quotedPropertyName,
                            "u8) : value.HasProperty(",
                            quotedPropertyName,
                            "))")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level > ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent(
                                    "result = result.PushValidationLocationReducedPathModifierAndProperty(new(",
                                    SymbolDisplay.FormatLiteral(dependentSchema.KeywordPathModifier, true),
                                    "), ",
                                    quotedPropertyName,
                                    ");")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent(
                                "ValidationContext ",
                                resultName,
                                " = value.As<",
                                dependentSchema.ReducedDepdendentSchemaType.FullyQualifiedDotnetTypeName(),
                                ">().Validate(result.CreateChildContext(), level);")
                            .AppendLineIndent("if (level == ValidationLevel.Flag && !", resultName, ".IsValid)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                    .AppendLineIndent("return ValidationContext.InvalidContext;")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent("if (level > ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = result.PopLocation();")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent("result = result.MergeChildContext(", resultName, ", true);")
                        .PopIndent()
                        .AppendLineIndent("}");

                    ++i;
                }
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => false;

    /// <summary>
    /// Gets the required property hasSeen variable name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="property">The property declaration.</param>
    /// <returns>The required property hasSeen variable name.</returns>
    internal static string GetHasSeenVariableName(CodeGenerator generator, PropertyDeclaration property)
    {
        return generator.GetVariableNameInScope(property.DotnetPropertyName(), prefix: "hasSeen");
    }
}