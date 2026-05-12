// <copyright file="CodeGeneratorExtensions.Documentation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Web;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generator extensions for XML documentation generation.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Append documentation for the type declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append documentation.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendDocumentation(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ShortDocumentation() is string shortDocumentation)
        {
            generator.AppendSummary(shortDocumentation);
        }
        else
        {
            generator.AppendSummary("Generated from JSON Schema.");
        }

        return generator
            .AppendRemarks(typeDeclaration.LongDocumentation(), typeDeclaration.Examples());
    }

    /// <summary>
    /// Append the text as a code-formatted example.
    /// </summary>
    /// <param name="generator">The generator to which to append the example.</param>
    /// <param name="example">The example to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExample(this CodeGenerator generator, string example)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendLineIndent("/// <example>")
            .AppendLineIndent("/// <code>")
            .AppendBlockIndentWithPrefix(HttpUtility.HtmlEncode(example), "/// ")
            .AppendLineIndent("/// </code>")
            .AppendLineIndent("/// </example>");
    }

    /// <summary>
    /// Append the examples as a code-formatted examples paragraph.
    /// </summary>
    /// <param name="generator">The generator to which to append the example.</param>
    /// <param name="examples">The examples to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExamples(this CodeGenerator generator, string[] examples)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendLineIndent("/// <para>")
            .AppendLineIndent("/// Examples:");

        foreach (string example in examples)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator.AppendExample(example);
        }

        return generator
            .AppendLineIndent("/// </para>");
    }

    /// <summary>
    /// Appends remarks, ensuring that the summary text is correctly formatted.
    /// </summary>
    /// <param name="generator">The generator to which to append the summary.</param>
    /// <param name="longDocumentation">The (optional) long documentation text.</param>
    /// <param name="documentationExamples">The (optional) array of examples text.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// If neither long documentation nor examples are present, then this will not append a remarks section.
    /// </remarks>
    public static CodeGenerator AppendRemarks(this CodeGenerator generator, string? longDocumentation, string[]? documentationExamples)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (longDocumentation is null && documentationExamples is null)
        {
            return generator;
        }

        generator.AppendLineIndent("/// <remarks>");

        if (longDocumentation is string docs)
        {
            generator.AppendParagraphs(docs);
        }

        if (documentationExamples is string[] examples)
        {
            generator.AppendExamples(examples);
        }

        return generator
            .AppendLineIndent("/// </remarks>");
    }

    /// <summary>
    /// Appends a summary, ensuring that the summary text is correctly formatted.
    /// </summary>
    /// <param name="generator">The generator to which to append the summary.</param>
    /// <param name="summaryText">The summary text to append. Multiple lines will be correctly indented.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSummary(this CodeGenerator generator, string summaryText)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendLineIndent("/// <summary>")
            .AppendBlockIndentWithPrefix(HttpUtility.HtmlEncode(summaryText), "/// ")
            .AppendLineIndent("/// </summary>");
    }

    /// <summary>
    /// Append <c>&lt;see cref="[typeName]"/&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeName">The type name to which to append the reference.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTypeAsSeeCref(
        this CodeGenerator generator,
        string typeName)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .Append("<see cref=\"");

        foreach (char c in typeName)
        {
            if (c == '<')
            {
                generator.Append('{');
            }
            else if (c == '>')
            {
                generator.Append('}');
            }
            else
            {
                generator.Append(c);
            }
        }

        return generator
            .Append("\"/>");
    }
}