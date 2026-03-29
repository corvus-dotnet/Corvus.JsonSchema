// <copyright file="JsonRegexNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Represents a node in a JSON regular expression parse tree.
/// </summary>
internal readonly struct JsonRegexNode
{
    /// <summary>
    /// Arbitrary number of repetitions of the same character when we'd prefer to represent that as a repeater of that character rather than a string.
    /// </summary>
    internal const int MultiVsRepeaterLimit = 64;

    private readonly int _idx;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRegexNode"/> struct.
    /// </summary>
    /// <param name="index">The index of the node in the validator's internal collection.</param>
    internal JsonRegexNode(int index)
    {
        _idx = index;
    }

    /// <summary>
    /// Gets a null regex node instance.
    /// </summary>
    internal static JsonRegexNode Null { get; } = new JsonRegexNode(-1);

    /// <summary>
    /// Gets a value indicating whether this node is null.
    /// </summary>
    internal readonly bool IsNull => _idx < 0;

    /// <summary>
    /// Adds a child node to this node.
    /// </summary>
    /// <param name="validator">The regex validator managing the node relationships.</param>
    /// <param name="child">The child node to add.</param>
    public void AddChild(ref JsonRegexValidator validator, in JsonRegexNode child)
    {
        validator.SetParent(child._idx, _idx);
    }

    /// <summary>
    /// Gets the number of child nodes for this node.
    /// </summary>
    /// <param name="validator">The regex validator managing the node data.</param>
    /// <returns>The number of child nodes.</returns>
    public readonly int GetChildCount(ref JsonRegexValidator validator) => validator.GetChildCount(_idx);

    /// <summary>
    /// Gets the kind of this regex node.
    /// </summary>
    /// <param name="validator">The regex validator managing the node data.</param>
    /// <returns>The node kind.</returns>
    public readonly JsonRegexNodeKind GetNodeKind(ref JsonRegexValidator validator) => validator.GetNodeKind(_idx);

    /// <summary>
    /// Gets the parent node of this node.
    /// </summary>
    /// <param name="validator">The regex validator managing the node relationships.</param>
    /// <returns>The parent node.</returns>
    public readonly JsonRegexNode GetParent(ref JsonRegexValidator validator) => validator.GetParent(_idx);

    /// <summary>
    /// Sets the parent of this node.
    /// </summary>
    /// <param name="validator">The regex validator managing the node relationships.</param>
    /// <param name="parent">The parent node to set.</param>
    public void SetParent(ref JsonRegexValidator validator, in JsonRegexNode parent) => validator.SetParent(_idx, parent._idx);

    /// <summary>
    /// Creates a quantifier node from this node.
    /// </summary>
    /// <param name="validator">The regex validator managing the node data.</param>
    /// <param name="lazy">Whether the quantifier is lazy.</param>
    /// <param name="min">The minimum number of repetitions.</param>
    /// <param name="max">The maximum number of repetitions.</param>
    /// <returns>A quantifier node or optimized equivalent.</returns>
    internal readonly JsonRegexNode MakeQuantifier(ref JsonRegexValidator validator, bool lazy, int min, int max)
    {
        // Certain cases of repeaters (min == max) can be handled specially
        if (min == max)
        {
            switch (max)
            {
                case 0:

                    // The node is repeated 0 times, so it's actually empty.
                    return validator.CreateNode(JsonRegexNodeKind.Empty);

                case 1:

                    // The node is repeated 1 time, so it's not actually a repeater.
                    return this;

                case <= MultiVsRepeaterLimit when GetNodeKind(ref validator) == JsonRegexNodeKind.One:

                    // The same character is repeated a fixed number of times, so it's actually a multi.
                    // While this could remain a repeater, multis are more readily optimized later in
                    // processing. The counts used here in real-world expressions are invariably small (e.g. 4),
                    // but we set an upper bound just to avoid creating really large strings.
                    Debug.Assert(max >= 2);
                    validator.SetKind(_idx, JsonRegexNodeKind.Multi);
                    return this;
            }
        }

        switch (GetNodeKind(ref validator))
        {
            case JsonRegexNodeKind.One:
            case JsonRegexNodeKind.Notone:
            case JsonRegexNodeKind.Set:
                MakeRep(ref validator, lazy ? JsonRegexNodeKind.Onelazy : JsonRegexNodeKind.Oneloop);
                return this;

            default:
                JsonRegexNode result = validator.CreateNode(lazy ? JsonRegexNodeKind.Lazyloop : JsonRegexNodeKind.Loop);
                result.AddChild(ref validator, this);
                return result;
        }
    }

    /// <summary>
    /// Pass type as OneLazy or OneLoop
    /// </summary>
    private void MakeRep(ref JsonRegexValidator validator, JsonRegexNodeKind kind)
    {
        JsonRegexNodeKind currentKind = validator.GetNodeKind(_idx);
        currentKind += kind - JsonRegexNodeKind.One;
        validator.SetKind(_idx, currentKind);
    }
}