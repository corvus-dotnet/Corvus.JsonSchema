// <copyright file="CodeGeneratorExtensions.Unions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Emits the partial declaration that makes a <c>oneOf</c>/<c>anyOf</c> composition type a C# union.
/// </summary>
/// <remarks>
/// <para>
/// A C# 15 compiler (the .NET 11 SDK) treats a type marked <c>[System.Runtime.CompilerServices.Union]</c> as a union
/// whose cases are declared by the static <c>Create</c> factories of a nested <c>IUnionMembers</c> interface; a
/// <c>switch</c> or <c>is</c> pattern over the case types is then exhaustive and, because the provider also declares
/// the non-boxing <c>HasValue</c>/<c>TryGetValue</c> members, never boxes. The cases and the probes are the same as
/// the type's <c>Match</c> method's: a case matches when its schema evaluates true for this element. The attribute is
/// polyfilled for target frameworks before .NET 11 (<see cref="Formatting.UnionAttributeFileName"/>), and older
/// compilers see ordinary members. The declaration is left out of .NET Framework targets, whose compiler refuses
/// interface members with bodies (CS8701).
/// </para>
/// </remarks>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends a second partial declaration of the type that makes it a C# union, if it is one: the
    /// <c>[Union]</c> attribute, the nested <c>IUnionMembers</c> provider and its explicit implementation, under
    /// <c>#if !NETFRAMEWORK</c>.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendUnionPartial(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.UnionCaseTypes() is not IReadOnlyList<TypeDeclaration> cases)
        {
            return generator;
        }

        string typeName = typeDeclaration.DotnetTypeName();
        string accessibility = typeDeclaration.DotnetAccessibility() == GeneratedTypeAccessibility.Internal ? "internal" : "public";

        generator
            .AppendSeparatorLine()
            .AppendLine("#if !NETFRAMEWORK")
            .AppendLineIndent("/// <content>")
            .AppendLineIndent("/// The type as a C# union: with a C# 15 compiler (the .NET 11 SDK or later) a <c>switch</c> or <c>is</c> pattern tests")
            .AppendLineIndent("/// the branch types directly. Not available when targeting .NET Framework.")
            .AppendLineIndent("/// </content>")
            .AppendLineIndent("[global::System.Runtime.CompilerServices.Union]")
            .AppendLineIndent(accessibility, " readonly partial struct ", typeName, " : ", typeName, ".IUnionMembers")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// The C# union members of this type: one case for each branch of the schema's composition, in schema order.")
            .AppendLineIndent("/// A value that matches no case has a <see langword=\"null\"/> <see cref=\"Value\"/>, which a <c>null</c> pattern handles.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("public interface IUnionMembers")
            .AppendLineIndent("{")
            .PushIndent();

        foreach (TypeDeclaration caseType in cases)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            string caseName = caseType.FullyQualifiedDotnetTypeName();
            generator
                .AppendLineIndent("/// <summary>Creates an instance of the union from a <see cref=\"", caseName, "\"/>.</summary>")
                .AppendLineIndent("/// <param name=\"value\">The value.</param>")
                .AppendLineIndent("/// <returns>The value as a <see cref=\"", typeName, "\"/>.</returns>")
                .AppendLineIndent("static ", typeName, " Create(", caseName, " value) => From(value);");
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>Gets the value as the first case type whose schema it matches, or <see langword=\"null\"/>.</summary>")
            .AppendLineIndent("object? Value { get; }")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>Gets a value indicating whether the value matches any case type.</summary>")
            .AppendLineIndent("bool HasValue { get; }");

        foreach (TypeDeclaration caseType in cases)
        {
            string caseName = caseType.FullyQualifiedDotnetTypeName();
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>Gets the value as a <see cref=\"", caseName, "\"/> if it matches that case.</summary>")
                .AppendLineIndent("/// <param name=\"value\">The value as the case type, if it matches.</param>")
                .AppendLineIndent("/// <returns><see langword=\"true\"/> if the value matches the case.</returns>")
                .AppendLineIndent("bool TryGetValue(out ", caseName, " value);");
        }

        generator
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("object? IUnionMembers.Value")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent();

        foreach (TypeDeclaration caseType in cases)
        {
            string caseName = caseType.FullyQualifiedDotnetTypeName();
            generator
                .AppendLineIndent("if (", caseName, ".", generator.JsonSchemaClassName(caseName), ".Evaluate(_parent, _idx))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return ", caseName, ".From(this);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine();
        }

        generator
                .AppendLineIndent("return null;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendIndent("bool IUnionMembers.HasValue =>");

        bool first = true;
        foreach (TypeDeclaration caseType in cases)
        {
            string caseName = caseType.FullyQualifiedDotnetTypeName();
            generator
                .AppendLine(first ? string.Empty : " ||")
                .PushIndent()
                .AppendIndent(caseName, ".", generator.JsonSchemaClassName(caseName), ".Evaluate(_parent, _idx)")
                .PopIndent();
            first = false;
        }

        generator.AppendLine(";");

        foreach (TypeDeclaration caseType in cases)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            string caseName = caseType.FullyQualifiedDotnetTypeName();
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <inheritdoc/>")
                .AppendLineIndent("bool IUnionMembers.TryGetValue(out ", caseName, " value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (", caseName, ".", generator.JsonSchemaClassName(caseName), ".Evaluate(_parent, _idx))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("value = ", caseName, ".From(this);")
                        .AppendLineIndent("return true;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("value = default;")
                    .AppendLineIndent("return false;")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLine("#endif");
    }
}