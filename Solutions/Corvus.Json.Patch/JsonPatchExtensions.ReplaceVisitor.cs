// <copyright file="JsonPatchExtensions.ReplaceVisitor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;
using Corvus.Json.Visitor;

/// <summary>
/// JSON Patch extensions.
/// </summary>
public static partial class JsonPatchExtensions
{
    private class ReplaceVisitor
    {
        public ReplaceVisitor(JsonAny node, Replace patchOperation)
        {
            this.Document = node;
            this.PatchOperation = patchOperation;
        }

        public JsonAny Document { get; }

        public Replace PatchOperation { get; }

        public VisitResult Visit(ReadOnlySpan<char> path, JsonAny nodeToVisit)
        {
            return VisitForReplace(path, nodeToVisit, this.PatchOperation.Value, this.PatchOperation.Path);
        }

        internal static VisitResult VisitForReplace(ReadOnlySpan<char> path, JsonAny nodeToVisit, JsonAny value, ReadOnlySpan<char> operationPath)
        {
            // If we are the root, or our span starts with the path so far, we might be matching
            if (operationPath.Length == 0 || operationPath.StartsWith(path))
            {
                if (operationPath.Length == path.Length)
                {
                    // We are an exact match, so we can just replace this node.
                    return new(value, Transformed.Yes, Walk.TerminateAtThisNodeAndKeepChanges);
                }

                // Otherwise we need to continue, as we are on the path
                return new(nodeToVisit, Transformed.No, Walk.Continue);
            }

            // If it didn't start with the span, we can give up on this whole tree segment
            return new(nodeToVisit, Transformed.No, Walk.SkipChildren);
        }
    }
}
