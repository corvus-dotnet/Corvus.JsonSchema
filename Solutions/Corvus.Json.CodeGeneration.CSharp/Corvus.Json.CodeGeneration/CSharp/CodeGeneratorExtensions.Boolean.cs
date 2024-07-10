// <copyright file="CodeGeneratorExtensions.Boolean.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends the <c>TryGetBoolean()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetBoolean(this CodeGenerator generator)
    {
        return generator
            .ReserveName("TryGetBoolean")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Try to retrieve the value as a boolean.
            /// </summary>
            /// <param name="result"><see langword="true"/> if the value was true, otherwise <see langword="false"/>.</param>
            /// <returns><see langword="true"/> if the value was representable as a boolean, otherwise <see langword="false"/>.</returns>
            public bool TryGetBoolean([NotNullWhen(true)] out bool result)
            {
                switch (this.ValueKind)
                {
                    case JsonValueKind.True:
                        result = true;
                        return true;
                    case JsonValueKind.False:
                        result = false;
                        return true;
                    default:
                        result = default;
                        return false;
                }
            }
            """);
    }

    /// <summary>
    /// Appends the <c>GetBoolean()</c> method.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendGetBoolean(this CodeGenerator generator)
    {
        return generator
            .ReserveName("GetBoolean")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Get the value as a boolean.
            /// </summary>
            /// <returns>The value of the boolean, or <see langword="null"/> if the value was not representable as a boolean.</returns>
            public bool? GetBoolean()
            {
                if (this.TryGetBoolean(out bool result))
                {
                    return result;
                }

                return null;
            }
            """);
    }
}