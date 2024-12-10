// <copyright file="VariableSpecification.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.HighPerformance;

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
    /// <param name="first">Whether this is the first variable in the template.</param>
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
    public void CopyTo(ref ValueStringBuilder output)
    {
        if (this.First)
        {
            if (this.OperatorInfo.First != '\0')
            {
                output.Append(this.OperatorInfo.First);
            }
        }

        output.Append(this.VarName);

        if (this.Explode)
        {
            output.Append('*');
        }

        if (this.PrefixLength > 0)
        {
            output.Append(':');

            int digits = this.PrefixLength == 0 ? 1 : (int)Math.Floor(Math.Log10(this.PrefixLength)) + 1;

            Span<char> buffer = output.AppendSpan(digits);

#if NET8_0_OR_GREATER
            this.PrefixLength.TryFormat(buffer, out int charsWritten);
#else
            TryFormat(this.PrefixLength, buffer, digits);
#endif
        }

#if !NET8_0_OR_GREATER
        static void TryFormat(int value, Span<char> span, int digits)
        {
            while (digits > 0)
            {
                digits--;
                span[digits] = (char)((value % 10) + '0');
                value /= 10;
            }
        }
#endif
    }

    /// <summary>
    /// Gets the variable as a string.
    /// </summary>
    /// <returns>The variable specification as a string.</returns>
    public override string ToString()
    {
        int maximumOutputLength = 3 + this.VarName.Length + this.PrefixLength;
        const int MaximumSupportedOutputLength = 4096;
        if (maximumOutputLength > MaximumSupportedOutputLength)
        {
            // Entire URIs typically shouldn't be longer than this, so something is odd if we exceed this.
            // This is a safety-oriented policy. If someone turns out to come up with a reasonable
            // scenario in which they genuinely need more, we can revisit this.
            throw new InvalidOperationException($"The length of this variable would exceed {MaximumSupportedOutputLength} characters. This is unlikely to be correct, but if this limit is unacceptable to you please file an issue at https://github.com/corvus-dotnet/Corvus.UriTemplates/issues");
        }

        Span<char> initialBuffer = stackalloc char[maximumOutputLength];
        ValueStringBuilder builder = new(initialBuffer);
        if (this.First)
        {
            builder.Append(this.OperatorInfo.First);
        }

#if NET8_0_OR_GREATER
        builder.Append(this.VarName);
#else
        foreach (char c in this.VarName)
        {
            builder.Append(c);
        }
#endif
        if (this.Explode)
        {
            builder.Append('*');
        }

        if (this.PrefixLength > 0)
        {
            builder.Append(':');
            builder.Append(this.PrefixLength);
        }

        return builder.CreateStringAndDispose();
    }
}