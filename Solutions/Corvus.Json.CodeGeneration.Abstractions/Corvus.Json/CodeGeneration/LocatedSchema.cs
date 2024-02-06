// <copyright file="LocatedSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A located schema.
/// </summary>
public class LocatedSchema
{
    private readonly Dictionary<string, Anchor> anchors = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LocatedSchema"/> class.
    /// </summary>
    /// <param name="location">The scoped location of the located schema.</param>
    /// <param name="schema">The JSON schema at the location.</param>
    internal LocatedSchema(JsonReference location, JsonAny schema)
    {
        this.Location = location;
        this.Schema = schema;
    }

    private LocatedSchema(JsonReference location, JsonAny schema, Dictionary<string, Anchor> anchors)
    {
        this.Location = location;
        this.Schema = schema;
        this.anchors = anchors;
    }

    /// <summary>
    /// Gets the scoped location of the located schema.
    /// </summary>
    public JsonReference Location { get; internal set;  }

    /// <summary>
    /// Gets the schema associated with the location.
    /// </summary>
    public JsonAny Schema { get; }

    /// <summary>
    /// Gets the named anchors for the located schema.
    /// </summary>
    internal IEnumerable<(string Name, Anchor Anchor)> NamedAnchors => this.anchors.Select(kvp => (kvp.Key, kvp.Value));

    /// <summary>
    ///  Gets or sets a value indicating whether this schema has a recursive anchor.
    /// </summary>
    internal bool IsRecursiveAnchor { get; set; }

    /// <summary>
    /// Gets a value indicating whether the schema has an anchor with the given name.
    /// </summary>
    /// <param name="anchorName">The name of the anchor.</param>
    /// <returns><see langword="true"/> if the schema has an anchor of the specified name.</returns>
    internal bool HasAnchor(string anchorName)
    {
        return this.anchors.ContainsKey(anchorName);
    }

    /// <summary>
    /// Add an anchor to the located schema.
    /// </summary>
    /// <param name="anchorName">The name of the anchor.</param>
    /// <param name="subschema">The subschema to add for the anchor.</param>
    /// <returns><see langword="true"/> if the anchor was added, <see langword="false"/> if an anchor with this name already existed.</returns>
    internal bool TryAddAnchor(string anchorName, LocatedSchema subschema)
    {
        return this.anchors.TryAdd(anchorName, new Anchor(subschema, false));
    }

    /// <summary>
    /// Add a dynamic anchor to the located schema.
    /// </summary>
    /// <param name="anchorName">The name of the anchor.</param>
    /// <param name="subschema">The subschema to add or update for the anchor.</param>
    /// <remarks>
    /// This will add a dynamic anchor of the given name, if the anchor does not already exist.
    /// If it does, it will be updated to be a dynamic anchor.</remarks>
    internal void AddOrUpdateDynamicAnchor(string anchorName, LocatedSchema subschema)
    {
        this.anchors.Remove(anchorName);
        this.anchors.TryAdd(anchorName, new Anchor(subschema, true));
    }

    /// <summary>
    /// Attempts to get the named anchor from the registration.
    /// </summary>
    /// <param name="anchor">The name of the anchor.</param>
    /// <param name="registeredAnchor">The anchor registered for that name.</param>
    /// <returns><see langword="true"/> when the anchor is found, otherwise false.</returns>
    internal bool TryGetAnchor(string anchor, [NotNullWhen(true)] out Anchor? registeredAnchor)
    {
        return this.anchors.TryGetValue(anchor, out registeredAnchor);
    }

    /// <summary>
    /// Returns a new located schema with an updated location.
    /// </summary>
    /// <param name="location">The updated location.</param>
    /// <returns>The located schema with the updated location.</returns>
    internal LocatedSchema WithLocation(JsonReference location)
    {
        return new LocatedSchema(location, this.Schema, this.anchors);
    }

    /// <summary>
    /// Determines whether the schema has a dynamic anchor.
    /// </summary>
    /// <returns><see langword="true"/> if the schema contains a dynamic anchor.</returns>
    internal bool HasDynamicAnchor()
    {
        return this.anchors.Any(a => a.Value.IsDynamic);
    }
}