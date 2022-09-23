// <copyright file="VariableSpecification.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using CommunityToolkit.HighPerformance;
using Corvus.UriTemplates.Internal;

namespace Corvus.UriTemplates.TemplateParameterProviders;

/// <summary>
/// A variable specification.
/// </summary>
public ref struct VariableSpecification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VariableSpecification"/> struct.
    /// </summary>
    /// <param name="operatorInfo">The operator info.</param>
    /// <param name="variableName">The variable name.</param>
    /// <param name="explode">Whether to explode the variable.</param>
    /// <param name="prefixLength">The prefix length.</param>
    /// <param name="first">Wether this is the first variable in the template.</param>
    public VariableSpecification(OperatorInfo operatorInfo, ReadOnlySpan<char> variableName, bool explode = false, int prefixLength = 0, bool first = true)
    {
        this.OperatorInfo = operatorInfo;
        this.Explode = explode;
        this.PrefixLength = prefixLength;
        this.First = first;
        this.VarName = variableName;
    }

    /// <summary>
    /// Gets the operation info.
    /// </summary>
    public OperatorInfo OperatorInfo { get; }

    /// <summary>
    /// Gets or sets the variable name.
    /// </summary>
    public ReadOnlySpan<char> VarName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this variable is exploded.
    /// </summary>
    public bool Explode { get; set; }

    /// <summary>
    /// Gets or sets the prefix length for the variable.
    /// </summary>
    public int PrefixLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the first variable in the template.
    /// </summary>
    public bool First { get; set; }

    /// <summary>
    /// Copy the result to the output span.
    /// </summary>
    /// <param name="output">The span to which to copy the result.</param>
    public void CopyTo(IBufferWriter<char> output)
    {
        int written = 0;
        if (this.First)
        {
            if (this.OperatorInfo.First != '\0')
            {
                output.Write(this.OperatorInfo.First);
            }
        }

        output.Write(this.VarName);
        written += this.VarName.Length;

        if (this.Explode)
        {
            output.Write('*');
        }

        if (this.PrefixLength > 0)
        {
            output.Write(':');

            Span<char> buffer = stackalloc char[256];
            this.PrefixLength.TryFormat(buffer, out int charsWritten);
            output.Write(buffer[..charsWritten]);
        }
    }

    /// <summary>
    /// Gets the variable as a string.
    /// </summary>
    /// <returns>The variable specification as a string.</returns>
    public override string ToString()
    {
        StringBuilder builder = StringBuilderPool.Shared.Get();
        if (this.First)
        {
            builder.Append(this.OperatorInfo.First);
        }

        builder.Append(this.VarName);
        if (this.Explode)
        {
            builder.Append('*');
        }

        if (this.PrefixLength > 0)
        {
            builder.Append(':');
            builder.Append(this.PrefixLength);
        }

        string result = builder.ToString();
        StringBuilderPool.Shared.Return(builder);
        return result;
    }
}