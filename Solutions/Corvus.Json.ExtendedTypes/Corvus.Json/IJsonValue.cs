// <copyright file="IJsonValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// A JSON value.
/// </summary>
public interface IJsonValue
{
    /// <summary>
    /// Gets the value as a <see cref="JsonAny"/>.
    /// </summary>
    JsonAny AsAny { get; }

    /// <summary>
    /// Gets the value as a <see cref="JsonString"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value was not a string.</exception>
    JsonString AsString { get; }

    /// <summary>
    /// Gets the value as a <see cref="JsonNumber"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value was not a number.</exception>
    JsonNumber AsNumber { get; }

    /// <summary>
    /// Gets the value as a <see cref="JsonObject"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value was not an object.</exception>
    JsonObject AsObject { get; }

    /// <summary>
    /// Gets the value as a <see cref="JsonArray"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    JsonArray AsArray { get; }

    /// <summary>
    /// Gets the value as a <see cref="JsonBoolean"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value was not a boolean.</exception>
    JsonBoolean AsBoolean { get; }

    /// <summary>
    /// Gets the value as a <see cref="JsonElement"/>.
    /// </summary>
    JsonElement AsJsonElement { get; }

    /// <summary>
    /// Gets a value indicating whether the value has a json element backing.
    /// </summary>
    bool HasJsonElementBacking { get; }

    /// <summary>
    /// Gets a value indicating whether the value has a dotnet instance backing.
    /// </summary>
    bool HasDotnetBacking { get; }

    /// <summary>
    /// Gets the value kind of the element.
    /// </summary>
    JsonValueKind ValueKind { get; }

    /// <summary>
    /// Writes the instance to a <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to which to write the instance.</param>
    void WriteTo(Utf8JsonWriter writer);

    /// <summary>
    /// Gets the value as the target value.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target.</typeparam>
    /// <returns>An instance of the target type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    TTarget As<TTarget>()
        where TTarget : struct, IJsonValue<TTarget>;

    /// <summary>
    /// Compares with another value for equality.
    /// </summary>
    /// <typeparam name="T">The type of the item with which to compare.</typeparam>
    /// <param name="other">The instance with which to compare.</param>
    /// <returns><c>True</c> if the other insance is equal to this one.</returns>
    bool Equals<T>(T other)
        where T : struct, IJsonValue<T>;

    /// <summary>
    /// Validates the instance against its own schema.
    /// </summary>
    /// <param name="context">The current validation context.</param>
    /// <param name="validationLevel">The validation level. (Defaults to <see cref="ValidationLevel.Flag"/>).</param>
    /// <returns>The <see cref="ValidationContext"/> updated with the results from this validation operation.</returns>
    ValidationContext Validate(in ValidationContext context, ValidationLevel validationLevel = ValidationLevel.Flag);
}