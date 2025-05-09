﻿// <copyright file="ValidatePartial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Provides a Validate partial class for a type declaration.
/// </summary>
public sealed class ValidatePartial : ICodeFileBuilder
{
    private ValidatePartial()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="ValidatePartial"/> builder.
    /// </summary>
    public static ValidatePartial Instance { get; } = new();

    /// <inheritdoc/>
    public CodeGenerator EmitFile(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If we have any validation keywords, emit the validation file.
        if (typeDeclaration.ValidationKeywords().Count != 0)
        {
            FrameworkType addExplicitUsings = typeDeclaration.AddExplicitUsings() ? FrameworkType.All : FrameworkType.NotEmitted;

            generator
                .BeginFile(typeDeclaration, "Validate")
                    .AppendAutoGeneratedHeader()
                    .AppendLine()
                    .AppendLine("#nullable enable")
                    .AppendLine()
                    .AppendUsings(
                        new("global::System", addExplicitUsings),
                        new("global::System.Collections.Generic", addExplicitUsings),
                        new("global::System.IO", addExplicitUsings),
                        new("global::System.Linq", addExplicitUsings),
                        new("global::System.Net.Http", addExplicitUsings),
                        new("global::System.Threading", addExplicitUsings),
                        new("global::System.Threading.Tasks", addExplicitUsings),
                        "System.Runtime.CompilerServices",
                        "System.Text.Json",
                        RequiresRegularExpressions(typeDeclaration) ? "System.Text.RegularExpressions" : ConditionalCodeSpecification.DoNotEmit,
                        new("Corvus.Json", EmitIfNotCorvusJsonExtendedType(typeDeclaration)))
                    .AppendLine()
                    .BeginTypeDeclarationNesting(typeDeclaration)
                        .AppendDocumentation(typeDeclaration)
                        .BeginReadonlyPartialStructDeclaration(
                            typeDeclaration.DotnetAccessibility(),
                            typeDeclaration.DotnetTypeName());

            // This is a core type
            if (typeDeclaration.IsCorvusJsonExtendedType())
            {
                generator
                                .AppendValidateMethod(typeDeclaration);
            }
            else
            {
                generator
                                .PushValidationClassNameAndScope()
                                .PushValidationHandlerMethodNames(typeDeclaration)
                                .AppendConstInstanceStaticProperty(typeDeclaration)
                                .AppendValidateMethod(typeDeclaration)
                                .AppendAnyOfConstantValuesClasses(typeDeclaration)
                                .AppendValidationClass(typeDeclaration)
                                .PopValidationHandlerMethodNames(typeDeclaration)
                                .PopValidationClassNameAndScope();
            }

            generator
                        .EndClassOrStructDeclaration()
                    .EndTypeDeclarationNesting(typeDeclaration)
                    .EndNamespace()
                .EndFile(typeDeclaration, "Validate");
        }

        return generator;

        static FrameworkType EmitIfNotCorvusJsonExtendedType(TypeDeclaration typeDeclaration)
        {
            return typeDeclaration.IsCorvusJsonExtendedType()
                 ? FrameworkType.NotEmitted
                 : FrameworkType.All;
        }
    }

    private static bool RequiresRegularExpressions(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.ValidationRegularExpressions() is IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>> regexes && regexes.Count != 0;
    }
}