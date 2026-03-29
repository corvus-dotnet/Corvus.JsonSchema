// <copyright file="LocatedSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A located schema.
/// </summary>
[DebuggerDisplay("{Location} - {Schema}")]
public class LocatedSchema
{
    private readonly List<ILocatedAnchor> anchors = [];
    private readonly Lazy<IAnchorKeyword?> anchorKeyword;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocatedSchema"/> class.
    /// </summary>
    /// <param name="location">The scoped location of the located schema.</param>
    /// <param name="schema">The JSON schema at the location.</param>
    /// <param name="vocabulary">The JSON schema vocabulary associated with the location.</param>
    internal LocatedSchema(JsonReference location, JsonElement schema, IVocabulary vocabulary)
    {
        this.Location = location;
        this.Schema = schema;
        this.Vocabulary = vocabulary;
        this.anchorKeyword = new(this.GetAnchorKeyword);
    }

    private LocatedSchema(JsonReference location, JsonElement schema, List<ILocatedAnchor> anchors, IVocabulary vocabulary)
    {
        this.Location = location;
        this.Schema = schema;
        this.Vocabulary = vocabulary;
        this.anchors = anchors;
        this.anchorKeyword = new(this.GetAnchorKeyword);
    }

    /// <summary>
    /// Gets the scoped location of the located schema.
    /// </summary>
    public JsonReference Location { get; internal set; }

    /// <summary>
    /// Gets the schema associated with the location.
    /// </summary>
    public JsonElement Schema { get; }

    /// <summary>
    /// Gets the schema vocabulary associated with the location.
    /// </summary>
    public IVocabulary Vocabulary { get; }

    /// <summary>
    /// Gets the <see cref="IAnchorKeyword"/> associated with this located schema,
    /// if any.
    /// </summary>
    public IAnchorKeyword? Anchor => this.anchorKeyword.Value;

    /// <summary>
    /// Gets the located anchors for the located schema.
    /// </summary>
    public IEnumerable<ILocatedAnchor> LocatedAnchors => this.anchors;

    /// <summary>
    /// Gets a value indicating whether the schema is a boolean schema.
    /// </summary>
    public bool IsBooleanSchema => this.Schema.ValueKind == JsonValueKind.True || this.Schema.ValueKind == JsonValueKind.False;

    /// <summary>
    /// Adds a located anchor to the set of anchors associated with this located schema.
    /// </summary>
    /// <param name="anchor">The anchor to add or updated.</param>
    public void AddOrUpdateLocatedAnchor(ILocatedAnchor anchor)
    {
        this.anchors.RemoveAll(item => item.Equals(anchor));
        this.anchors.Add(anchor);
    }

    /// <summary>
    /// Returns a new located schema with an updated location.
    /// </summary>
    /// <param name="location">The updated location.</param>
    /// <returns>The located schema with the updated location.</returns>
    public LocatedSchema WithLocation(JsonReference location)
    {
        return new LocatedSchema(location, this.Schema, this.anchors, this.Vocabulary);
    }

    private IAnchorKeyword? GetAnchorKeyword()
    {
        if (Anchors.TryGetAnchorKeyword(this.Schema, this.Vocabulary, out IAnchorKeyword? anchorKeyword))
        {
            return anchorKeyword;
        }

        return null;
    }
}