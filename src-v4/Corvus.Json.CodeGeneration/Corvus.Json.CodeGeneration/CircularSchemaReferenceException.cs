// <copyright file="CircularSchemaReferenceException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// The exception thrown when a schema contains a circular <em>same-instance</em> composition
/// (an <c>allOf</c>/<c>$ref</c>/<c>anyOf</c>/<c>oneOf</c> cycle) whose validation would never
/// terminate, and from which terminating code cannot be generated (issue #810).
/// </summary>
public sealed class CircularSchemaReferenceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircularSchemaReferenceException"/> class.
    /// </summary>
    /// <param name="referencingLocation">The location of the schema that holds the offending composition reference.</param>
    /// <param name="referencedLocation">The location of the schema it references, which is already being evaluated for the same instance.</param>
    public CircularSchemaReferenceException(JsonReference referencingLocation, JsonReference referencedLocation)
        : base(BuildMessage(referencingLocation, referencedLocation))
    {
        this.ReferencingLocation = referencingLocation.ToString();
        this.ReferencedLocation = referencedLocation.ToString();
    }

    /// <summary>
    /// Gets the location of the schema that holds the offending composition reference (the source of the cycle).
    /// </summary>
    public string ReferencingLocation { get; }

    /// <summary>
    /// Gets the location of the schema it references, which is already being evaluated for the same instance (the target of the cycle).
    /// </summary>
    public string ReferencedLocation { get; }

    private static string BuildMessage(JsonReference referencingLocation, JsonReference referencedLocation)
    {
        return
            $"Circular schema reference: the schema at '{referencingLocation}' has a same-instance composition " +
            $"reference (allOf/$ref/anyOf/oneOf) to '{referencedLocation}', which is already being evaluated for " +
            "the same instance. Validation of this schema would never terminate, so terminating code cannot be " +
            "generated. Break the cycle before generating types (for example, restructure the discriminated union " +
            "so its branches do not $ref back to the union).";
    }
}