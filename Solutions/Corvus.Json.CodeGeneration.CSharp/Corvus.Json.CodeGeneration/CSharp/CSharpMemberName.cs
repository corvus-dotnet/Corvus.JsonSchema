// <copyright file="CSharpMemberName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// The name of a C# member.
/// </summary>
public class CSharpMemberName(
    string fullyQualifiedScope,
    string baseName,
    Casing casing,
    string? prefix = null,
    string? suffix = null)
    : MemberName(fullyQualifiedScope, baseName, casing, prefix, suffix)
{
    private static ReadOnlySpan<char> PascalPrefix => "V".AsSpan();

    private static ReadOnlySpan<char> CamelPrefix => "v".AsSpan();

    /// <inheritdoc/>
    public override string BuildName()
    {
        string baseName = string.IsNullOrWhiteSpace(this.BaseName) ? "Empty" : this.BaseName;
        string prefix = string.IsNullOrWhiteSpace(this.Prefix) ? string.Empty : this.Prefix;
        string suffix = string.IsNullOrWhiteSpace(this.Suffix) ? string.Empty : this.Suffix;

        ReadOnlySpan<char> leadingDigitPrefix = this.Casing == Casing.PascalCase ? PascalPrefix : CamelPrefix;

        int bufferLength = Formatting.GetBufferLength(baseName.Length + prefix.Length + suffix.Length, leadingDigitPrefix);

        Span<char> buffer = stackalloc char[bufferLength];

        int totalLength = 0;

        // Copy the components into the buffer
        if (this.Casing == Casing.Unmodified)
        {
            totalLength = prefix.Length + baseName.Length + suffix.Length;
            prefix.AsSpan().CopyTo(buffer);
            baseName.AsSpan().CopyTo(buffer[prefix.Length..]);
            suffix.AsSpan().CopyTo(buffer[(prefix.Length + baseName.Length)..]);
        }
        else
        {
            if (this.Casing == Casing.PascalCase)
            {
                int written = 0;
                if (prefix.Length > 0)
                {
                    prefix.AsSpan().CopyTo(buffer);
                    written = Formatting.ToPascalCase(buffer[..prefix.Length]);
                    totalLength += written;
                }

                baseName.AsSpan().CopyTo(buffer[totalLength..]);
                written = Formatting.ToPascalCase(buffer.Slice(written, baseName.Length));
                totalLength += written;
            }
            else if (this.Casing == Casing.CamelCase)
            {
                int written = 0;
                if (prefix.Length > 0)
                {
                    prefix.AsSpan().CopyTo(buffer);
                    written = Formatting.ToCamelCase(buffer[..prefix.Length]);
                    totalLength += written;

                    baseName.AsSpan().CopyTo(buffer[totalLength..]);
                    written = Formatting.ToPascalCase(buffer.Slice(written, baseName.Length));
                    totalLength += written;
                }
                else
                {
                    baseName.AsSpan().CopyTo(buffer[totalLength..]);
                    written = Formatting.ToCamelCase(buffer.Slice(written, baseName.Length));
                    totalLength += written;
                }
            }

            if (suffix.Length > 0)
            {
                suffix.AsSpan().CopyTo(buffer[totalLength..]);
                totalLength += Formatting.ToPascalCase(buffer[(prefix.Length + baseName.Length)..]);
            }

            totalLength = Formatting.FixReservedWords(buffer, totalLength, leadingDigitPrefix);
        }

        return buffer[..totalLength].ToString();
    }
}