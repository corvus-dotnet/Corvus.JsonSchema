// <copyright file="GeneratorConfig.DisableOptionalNameHeuristicsEntity.Boolean.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

#nullable enable

using global::System.Diagnostics.CodeAnalysis;
using global::System.Text.Json;
using global::Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// JSON Schema for a configuration driver file for the corvus codegenerator.
/// </summary>
public readonly partial struct GeneratorConfig
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If true, do not use optional name heuristics.
    /// </para>
    /// <para>
    /// Examples:
    /// <example>
    /// <code>
    /// false
    /// </code>
    /// </example>
    /// </para>
    /// </remarks>
    public readonly partial struct DisableOptionalNameHeuristicsEntity
        : IJsonBoolean<Corvus.Json.CodeGenerator.GeneratorConfig.DisableOptionalNameHeuristicsEntity>
    {
        /// <summary>
        /// Conversion from <see cref="bool"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator DisableOptionalNameHeuristicsEntity(bool value)
        {
            return new(value);
        }

        /// <summary>
        /// Conversion from JsonBoolean.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator DisableOptionalNameHeuristicsEntity(JsonBoolean value)
        {
            if (value.HasDotnetBacking && (value.ValueKind == JsonValueKind.False || value.ValueKind == JsonValueKind.True))
            {
                return new(
                    (bool)value);
            }

            return new(value.AsJsonElement);
        }

        /// <summary>
        /// Conversion to JsonBoolean.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator JsonBoolean(DisableOptionalNameHeuristicsEntity value)
        {
            return
                value.AsBoolean;
        }

        /// <summary>
        /// Conversion to <see langword="bool"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        /// <exception cref="InvalidOperationException">The value was not a boolean.</exception>
        public static implicit operator bool(DisableOptionalNameHeuristicsEntity value)
        {
            return value.GetBoolean() ?? throw new InvalidOperationException();
        }

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
    }
}