// <copyright file="PropertyValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Internal;

/// <summary>
/// A delegate for property validators.
/// </summary>
/// <typeparam name="T">The type of the entity containing the property to validate.</typeparam>
/// <param name="that">An instnace of the entity containing the property to validate.</param>
/// <param name="validationContext">The validation context.</param>
/// <param name="level">The validation level.</param>
/// <returns>The updated validation context.</returns>
public delegate ValidationContext PropertyValidator<T>(in T that, in ValidationContext validationContext, ValidationLevel level)
    where T : struct, IJsonObject<T>;

/// <summary>
/// A delegate for property validators.
/// </summary>
/// <param name="that">An instnace of the entity containing the property to validate.</param>
/// <param name="validationContext">The validation context.</param>
/// <param name="level">The validation level.</param>
/// <returns>The updated validation context.</returns>
public delegate ValidationContext ObjectPropertyValidator(in JsonObjectProperty that, in ValidationContext validationContext, ValidationLevel level);