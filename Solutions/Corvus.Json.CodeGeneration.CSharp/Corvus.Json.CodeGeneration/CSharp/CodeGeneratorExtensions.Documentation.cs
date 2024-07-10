// <copyright file="CodeGeneratorExtensions.Documentation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
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
    /// Appends a summary, ensuring that the summary text is correctly formatted.
    /// </summary>
    /// <param name="generator">The generator to which to append the summary.</param>
    /// <param name="summaryText">The summary text to append. Multiple lines will be correctly indented.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSummary(this CodeGenerator generator, string summaryText)
    {
        return generator
            .AppendLineIndent("/// <summary>")
            .AppendBlockIndentWithPrefix(summaryText, "/// ")
            .AppendLineIndent("/// </summary>");
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
    /// Append the examples as a code-formatted examples paragraph.
    /// </summary>
    /// <param name="generator">The generator to which to append the example.</param>
    /// <param name="examples">The examples to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExamples(this CodeGenerator generator, string[] examples)
    {
        generator
            .AppendLineIndent("/// <para>")
            .AppendLineIndent("/// Examples:");

        foreach (string example in examples)
        {
            generator.AppendExample(example);
        }

        return generator
            .AppendLineIndent("/// </para>");
    }

    /// <summary>
    /// Append the text as a code-formatted example.
    /// </summary>
    /// <param name="generator">The generator to which to append the example.</param>
    /// <param name="example">The example to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExample(this CodeGenerator generator, string example)
    {
        return generator
            .AppendLineIndent("/// <example>")
            .AppendLineIndent("/// <code>")
            .AppendBlockIndentWithPrefix(example, "/// ")
            .AppendLineIndent("/// </code>")
            .AppendLineIndent("/// </example>");
    }
}