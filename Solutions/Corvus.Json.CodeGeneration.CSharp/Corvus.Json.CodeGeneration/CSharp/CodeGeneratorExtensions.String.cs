// <copyright file="CodeGeneratorExtensions.String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends an explicit conversion to string for a string-backed type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendExplicitConversionToString(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");
        return generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Conversion to string.
                /// </summary>
                /// <param name="value">The value from which to convert.</param>
                /// <exception cref="InvalidOperationException">The value was not a string.</exception>
                """)
            .AppendIndent("public static explicit operator string(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendBlockIndent(
                $$"""
                {
                    if ((value.{{backing}} & Backing.JsonElement) != 0)
                    {
                        if (value.{{jsonElementBacking}}.GetString() is string result)
                        {
                            return result;
                        }

                        throw new InvalidOperationException();
                    }

                    if ((value.{{backing}} & Backing.String) != 0)
                    {
                        return value.{{stringBacking}};
                    }

                    throw new InvalidOperationException();
                }
                """);
    }

    /// <summary>
    /// Append an implicit conversion to the well-known string format type
    /// if appropriate.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToStringFormat(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (WellKnownStringFormatHelpers.GetDotnetTypeNameFor(typeDeclaration.Format()) is string formatType)
        {
            return generator
                .AppendImplicitConversionFromJsonValueTypeUsingAs(typeDeclaration, formatType)
                .AppendImplicitConversionToJsonValueTypeUsingAs(typeDeclaration, formatType);
        }

        return generator;
    }

    /// <summary>
    /// Append a family of string concatenation functions.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendStringConcatenation(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        for (int i = 2; i <= 8; ++i)
        {
            generator.AppendSeparatorLine();
            AppendStringConcatenation(generator, typeDeclaration, i);
        }

        return generator;

        static void AppendStringConcatenation(CodeGenerator generator, TypeDeclaration typeDeclaration, int count)
        {
            generator
                .ReserveName("Concatenate")
                .AppendLineIndent("/// <summary>")
                .AppendIndent("/// Concatenate ")
                .Append(count)
                .Append(" JSON values, producing an instance of the string type ")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(".")
                .AppendLineIndent("/// </summary>");

            for (int i = 1; i <= count; ++i)
            {
                generator
                    .AppendIndent("/// <typeparam name=\"T")
                    .Append(i)
                    .Append("\">The type of the ")
                    .AppendOrdinalName(i)
                    .AppendLine(" value.</typeparam>");
            }

            generator
                .AppendLineIndent("/// <param name=\"buffer\">The buffer into which to concatenate the values.</param>");

            for (int i = 1; i <= count; ++i)
            {
                generator
                    .AppendIndent("/// <param name=\"value")
                    .Append(i)
                    .Append("\">The ")
                    .AppendOrdinalName(i)
                    .AppendLine(" value.</param>");
            }

            generator
                .AppendLineIndent("/// <returns>An instance of this string type.</returns>")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" Concatenate<");

            for (int i = 1; i <= count; ++i)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator
                    .Append('T')
                    .Append(i);
            }

            generator
                .Append(">(Span<byte> buffer, ");

            for (int i = 1; i <= count; ++i)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator
                    .Append("in T")
                    .Append(i)
                    .Append(" value")
                    .Append(i);
            }

            generator
                .AppendLine(")")
                .PushIndent();

            for (int i = 1; i <= count; ++i)
            {
                generator
                    .AppendIndent("where T")
                    .Append(i)
                    .Append(" : struct, IJsonValue<T")
                    .Append(i)
                    .AppendLine(">");
            }

            generator
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("int written = LowAllocJsonUtils.ConcatenateAsUtf8JsonString(buffer, ");

            for (int i = 1; i <= count; ++i)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator
                    .Append("value")
                    .Append(i);
            }

            generator
                .AppendLine(");")
                .AppendLineIndent("return ParseValue(buffer[..written]);")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Appends the <c>TryGetString()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetString(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");

        return generator
            .ReserveName("TryGetString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <inheritdoc/>
                public bool TryGetString([NotNullWhen(true)] out string? value)
                {
                    if ((this.{{backing}} & Backing.String) != 0)
                    {
                        value = this.{{stringBacking}};
                        return true;
                    }

                    if ((this.{{backing}} & Backing.JsonElement) != 0 &&
                        this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                    {
                        value = this.{{jsonElementBacking}}.GetString();
                        return value is not null;
                    }

                    value = null;
                    return false;
                }
                """);
    }

    /// <summary>
    /// Appends the <c>GetString()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetString(this CodeGenerator generator)
    {
        return generator
            .ReserveName("GetString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the string value.
                /// </summary>
                /// <returns><c>The string if this value represents a string</c>, otherwise <c>null</c>.</returns>
                public string? GetString()
                {
                    if (this.TryGetString(out string? value))
                    {
                        return value;
                    }

                    return null;
                }
                """);
    }

    /// <summary>
    /// Appends the <c>EqualsUtf8Bytes()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEqualsUtf8Bytes(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");

        return generator
            .ReserveName("EqualsUtf8Bytes")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <summary>
                /// Compare to a sequence of characters.
                /// </summary>
                /// <param name="utf8Bytes">The UTF8-encoded character sequence to compare.</param>
                /// <returns><c>True</c> if the sequences match.</returns>
                public bool EqualsUtf8Bytes(ReadOnlySpan<byte> utf8Bytes)
                {
                    if ((this.{{backing}} & Backing.JsonElement) != 0)
                    {
                        if (this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                        {
                            return this.{{jsonElementBacking}}.ValueEquals(utf8Bytes);
                        }
                    }

                    if ((this.{{backing}} & Backing.String) != 0)
                    {
                        int maxCharCount = Encoding.UTF8.GetMaxCharCount(utf8Bytes.Length);
                """)
            .PushIndent()
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
                    .AppendBlockIndent(
                        """
                        char[]? pooledChars = null;

                        Span<char> chars = maxCharCount <= JsonValueHelpers.MaxStackAlloc  ?
                            stackalloc char[maxCharCount] :
                            (pooledChars = ArrayPool<char>.Shared.Rent(maxCharCount));

                        try
                        {
                            int written = Encoding.UTF8.GetChars(utf8Bytes, chars);
                            return chars[..written].SequenceEqual(this.stringBacking);
                        }
                        finally
                        {
                            if (pooledChars is char[] pc)
                            {
                                ArrayPool<char>.Shared.Return(pc);
                            }
                        }
                        """)
                    .AppendLine("#else")
                    .AppendBlockIndent(
                        """
                        char[] chars = ArrayPool<char>.Shared.Rent(maxCharCount);
                        byte[] bytes = ArrayPool<byte>.Shared.Rent(utf8Bytes.Length);
                        utf8Bytes.CopyTo(bytes);

                        try
                        {
                            int written = Encoding.UTF8.GetChars(bytes, 0, bytes.Length, chars, 0);
                            return chars.SequenceEqual(this.stringBacking);
                        }
                        finally
                        {
                            ArrayPool<char>.Shared.Return(chars);
                            ArrayPool<byte>.Shared.Return(bytes);
                        }
                        """)
            .AppendLine("#endif")
            .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the <c>EqualsString()</c> method overloads.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEqualsString(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");
        string jsonElementBacking = generator.GetFieldNameInScope("jsonElementBacking");

        return generator
            .ReserveName("EqualsString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            $$"""
            /// <summary>
            /// Compare to a sequence of characters.
            /// </summary>
            /// <param name="chars">The character sequence to compare.</param>
            /// <returns><c>True</c> if the sequences match.</returns>
            public bool EqualsString(string chars)
            {
                if ((this.{{backing}} & Backing.JsonElement) != 0)
                {
                    if (this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                    {
                        return this.{{jsonElementBacking}}.ValueEquals(chars);
                    }

                    return false;
                }

                if ((this.{{backing}} & Backing.String) != 0)
                {
                    return chars.Equals(this.{{stringBacking}}, StringComparison.Ordinal);
                }

                return false;
            }

            /// <summary>
            /// Compare to a sequence of characters.
            /// </summary>
            /// <param name="chars">The character sequence to compare.</param>
            /// <returns><c>True</c> if the sequences match.</returns>
            public bool EqualsString(ReadOnlySpan<char> chars)
            {
                if ((this.{{backing}} & Backing.JsonElement) != 0)
                {
                    if (this.{{jsonElementBacking}}.ValueKind == JsonValueKind.String)
                    {
                        return this.{{jsonElementBacking}}.ValueEquals(chars);
                    }

                    return false;
                }

                if ((this.{{backing}} & Backing.String) != 0)
                {
            """)
            .PushIndent()
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
                    .AppendLineIndent($"return chars.SequenceEqual(this.{stringBacking});")
            .AppendLine("#else")
                    .AppendLineIndent($"return chars.SequenceEqual(this.{stringBacking}.AsSpan());")
            .AppendLine("#endif")
            .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends <c>TryFormat()</c> and <c>ToString()</c> methods for .NET 8.0 or greater.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNet80Formatting(this CodeGenerator generator)
    {
        string backing = generator.GetFieldNameInScope("backing");
        string stringBacking = generator.GetFieldNameInScope("stringBacking");

        return generator
            .ReserveName("__Corvus__Output")
            .ReserveNameIfNotReserved("TryFormat")
            .ReserveNameIfNotReserved("ToString")
            .AppendSeparatorLine()
            .AppendBlockIndentWithHashOutdent(
            $$"""
            #if NET8_0_OR_GREATER
            /// <inheritdoc/>
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            {
                if ((this.{{backing}} & Backing.String) != 0)
                {
                    int length = Math.Min(destination.Length, this.{{stringBacking}}.Length);
                    this.{{stringBacking}}.AsSpan(0, length).CopyTo(destination);
                    charsWritten = length;
                    return true;
                }

                if ((this.{{backing}} & Backing.JsonElement) != 0)
                {
                    char[] buffer = ArrayPool<char>.Shared.Rent(destination.Length);
                    try
                    {
                        bool result = this.jsonElementBacking.TryGetValue(FormatSpan, new __Corvus__Output(buffer, destination.Length), out charsWritten);
                        if (result)
                        {
                            buffer.AsSpan(0, charsWritten).CopyTo(destination);
                        }

                        return result;
                    }
                    finally
                    {
                        ArrayPool<char>.Shared.Return(buffer);
                    }
                }

                charsWritten = 0;
                return false;

                static bool FormatSpan(ReadOnlySpan<char> source, in __Corvus__Output output, out int charsWritten)
                {
                    int length = Math.Min(output.Length, source.Length);
                    source[..length].CopyTo(output.Destination);
                    charsWritten = length;
                    return true;
                }
            }

            /// <inheritdoc/>
            public string ToString(string? format, IFormatProvider? formatProvider)
            {
                // There is no formatting for the string
                return this.ToString();
            }

            private readonly record struct __Corvus__Output(char[] Destination, int Length);
            #endif
            """);
    }

    /// <summary>
    /// Appends the GetHashCode() and ToString() methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetHashCodeAndToStringMethods(this CodeGenerator generator)
    {
        return generator
            .ReserveNameIfNotReserved("GetHashCode")
            .ReserveNameIfNotReserved("ToString")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <inheritdoc/>
                public override int GetHashCode()
                {
                    return JsonValueHelpers.GetHashCode(this);
                }

                /// <inheritdoc/>
                public override string ToString()
                {
                    return this.Serialize();
                }
                """);
    }
}