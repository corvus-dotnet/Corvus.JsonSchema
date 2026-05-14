// <copyright file="IndentedWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// A simple indented text writer for code generation.
/// </summary>
internal sealed class IndentedWriter
{
    private readonly StringBuilder sb = new();
    private int indentLevel;
    private bool needsIndent = true;

    /// <summary>
    /// Gets or sets the indent string (default is 4 spaces).
    /// </summary>
    public string IndentString { get; set; } = "    ";

    /// <summary>
    /// Increases the indent level.
    /// </summary>
    /// <returns>This writer, for fluent chaining.</returns>
    public IndentedWriter PushIndent()
    {
        this.indentLevel++;
        return this;
    }

    /// <summary>
    /// Decreases the indent level.
    /// </summary>
    /// <returns>This writer, for fluent chaining.</returns>
    public IndentedWriter PopIndent()
    {
        if (this.indentLevel > 0)
        {
            this.indentLevel--;
        }

        return this;
    }

    /// <summary>
    /// Writes text at the current indent level.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <returns>This writer, for fluent chaining.</returns>
    public IndentedWriter Write(string text)
    {
        this.WriteIndentIfNeeded();
        this.sb.Append(text);
        return this;
    }

    /// <summary>
    /// Writes a line at the current indent level.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <returns>This writer, for fluent chaining.</returns>
    public IndentedWriter WriteLine(string text)
    {
        this.WriteIndentIfNeeded();
        this.sb.AppendLine(text);
        this.needsIndent = true;
        return this;
    }

    /// <summary>
    /// Writes an empty line.
    /// </summary>
    /// <returns>This writer, for fluent chaining.</returns>
    public IndentedWriter WriteLine()
    {
        this.sb.AppendLine();
        this.needsIndent = true;
        return this;
    }

    /// <summary>
    /// Opens a brace block: writes <c>{</c> and pushes indent.
    /// </summary>
    /// <returns>This writer, for fluent chaining.</returns>
    public IndentedWriter OpenBrace()
    {
        this.WriteLine("{");
        this.PushIndent();
        return this;
    }

    /// <summary>
    /// Closes a brace block: pops indent and writes <c>}</c>.
    /// </summary>
    /// <returns>This writer, for fluent chaining.</returns>
    public IndentedWriter CloseBrace()
    {
        this.PopIndent();
        this.WriteLine("}");
        return this;
    }

    /// <inheritdoc/>
    public override string ToString() => this.sb.ToString();

    private void WriteIndentIfNeeded()
    {
        if (this.needsIndent)
        {
            for (int i = 0; i < this.indentLevel; i++)
            {
                this.sb.Append(this.IndentString);
            }

            this.needsIndent = false;
        }
    }
}