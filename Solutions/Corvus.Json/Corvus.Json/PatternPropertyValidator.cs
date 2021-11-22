// <copyright file="PatternPropertyValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    /// <summary>
    /// A delegate for pattern property validators.
    /// </summary>
    /// <param name="that">An instance of the property to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The updated validation context.</returns>
    public delegate ValidationContext PatternPropertyValidator(in Property that, in ValidationContext validationContext, ValidationLevel level);
}
