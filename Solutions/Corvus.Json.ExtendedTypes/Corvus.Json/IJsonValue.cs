// <copyright file="IJsonValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System.Text.Json;

    /// <summary>
    /// Interface implemented by a JSON value.
    /// </summary>
    public interface IJsonValue
    {
        /// <summary>
        /// Gets the value as a JsonElement.
        /// </summary>
        /// <remarks>This will serialize a JsonElement instance if required. To avoid unnecessary
        /// serialization, use <see cref="HasJsonElement"/> to determine if the instance is backed by a <see cref="JsonElement"/>.</remarks>
        JsonElement AsJsonElement { get; }

        /// <summary>
        /// Gets a value indicating whether this is backed by a <see cref="JsonElement"/>.
        /// </summary>
        bool HasJsonElement { get; }

        /// <summary>
        /// Gets the <see cref="JsonValueKind"/>.
        /// </summary>
        JsonValueKind ValueKind { get; }

        /// <summary>
        /// Gets the instance as a JsonAny.
        /// </summary>
        JsonAny AsAny { get; }

        /// <summary>
        /// Writes the instance to a <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="writer">The writer to which to write the instance.</param>
        void WriteTo(Utf8JsonWriter writer);

        /// <summary>
        /// Compare this value with another.
        /// </summary>
        /// <typeparam name="T">The type of the other instance.</typeparam>
        /// <param name="other">The other instance.</param>
        /// <returns><c>True</c> if the values are equal.</returns>
        bool Equals<T>(T other)
            where T : struct, IJsonValue;

        /// <summary>
        /// Get this value as the given target type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>An instance of the given type.</returns>
        T As<T>()
            where T : struct, IJsonValue;

        /// <summary>
        /// Validate the value.
        /// </summary>
        /// <param name="validationContext">The intitial validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag);
    }
}